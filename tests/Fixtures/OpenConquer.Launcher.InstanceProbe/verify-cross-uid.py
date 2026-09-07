#!/usr/bin/env python3
"""Linux integration gate. Run as root on an isolated CI runner/container after Release build.

Uses the existing nobody/daemon accounts; creates no accounts and changes no host settings.
All children drop supplementary groups and UID/GID before executing product code or attacks.
"""

import hashlib
import os
from pathlib import Path
import pwd
import selectors
import shutil
import subprocess
import sys
import tempfile
import uuid


def require(condition, message):
    if not condition:
        raise AssertionError(message)


require(sys.platform == "linux" and os.geteuid() == 0,
        "This gate requires an isolated Linux runner with root for cross-UID children.")
victim = pwd.getpwnam("nobody")
attacker = pwd.getpwnam("daemon")
require(victim.pw_uid != attacker.pw_uid and victim.pw_uid != 0 and attacker.pw_uid != 0,
        "Cross-UID tests require two distinct unprivileged accounts.")
dotnet = shutil.which("dotnet")
require(dotnet is not None, "dotnet must be available.")
source = Path(sys.argv[1]).resolve()
require((source / "OpenConquer.Launcher.InstanceProbe.dll").is_file(), "Pass the built probe output directory.")
children = []
old_socket = Path("/tmp/oc-launcher-" + hashlib.sha256(victim.pw_name.encode()).hexdigest())
require(not os.path.lexists(old_socket), "The old test endpoint is occupied; refusing to modify it.")
old_created = False


with tempfile.TemporaryDirectory(prefix="ocuid-") as scratch:
    root = Path(scratch)
    root.chmod(0o755)
    shutil.copytree(source, root / "probe")
    probe = root / "probe/OpenConquer.Launcher.InstanceProbe.dll"

    def directory(name, owner=victim, mode=0o700):
        path = root / name
        path.mkdir()
        os.chown(path, owner.pw_uid, owner.pw_gid)
        path.chmod(mode)
        return path

    runtime = directory("runtime")
    home = directory("home")
    attacker_home = directory("attacker", attacker)

    def environment(account, runtime_path=runtime):
        return {
            "PATH": os.environ["PATH"],
            "HOME": str(home if account == victim else attacker_home),
            "DOTNET_CLI_HOME": str(home if account == victim else attacker_home),
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_NOLOGO": "1",
            "XDG_RUNTIME_DIR": str(runtime_path),
        }

    def start(account, command, runtime_path=runtime):
        process = subprocess.Popen(command, user=account.pw_uid, group=account.pw_gid,
                                   extra_groups=[], env=environment(account, runtime_path),
                                   cwd=root, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                   stderr=subprocess.PIPE, text=True)
        children.append(process)
        return process

    def run(account, command, runtime_path=runtime, success=True):
        process = start(account, command, runtime_path)
        stdout, stderr = process.communicate(timeout=20)
        require((process.returncode == 0) == success,
                f"UID {account.pw_uid}, exit {process.returncode}: {stdout} {stderr}")
        return stdout.strip()

    def resolve(runtime_path=runtime, success=True):
        return run(victim, [dotnet, str(probe), "resolve", "unused", "@current-user"],
                   runtime_path, success)

    def line(process):
        with selectors.DefaultSelector() as selector:
            selector.register(process.stdout, selectors.EVENT_READ)
            require(bool(selector.select(15)), "Timed out waiting for the instance probe.")
        result = process.stdout.readline().strip()
        require(bool(result), "Instance probe exited before producing its expected response.")
        return result

    def owner(lease, pipe="@current-user"):
        process = start(victim, [dotnet, str(probe), "listen", lease, pipe])
        require(line(process) == "ready", "Owner failed to start.")
        return process

    def activate(lease):
        require(run(victim, [dotnet, str(probe), "activate", lease, "@current-user"])
                == "ActivatedExisting", "Secondary process failed to activate the owner.")

    try:
        # Reproduce the attack from another UID before the victim resolves its endpoint.
        run(attacker, [sys.executable, "-c",
                      "import socket,sys; s=socket.socket(socket.AF_UNIX); s.bind(sys.argv[1])",
                      str(old_socket)])
        old_created = True
        require(old_socket.lstat().st_uid == attacker.pw_uid, "Old endpoint must belong to attacker.")
        pipe = Path(resolve())
        require(pipe == runtime / "openconquer/activate", "Unexpected private endpoint.")
        require(pipe.parent.stat().st_uid == victim.pw_uid, "Wrong application directory owner.")
        require(pipe.parent.stat().st_mode & 0o7777 == 0o700, "Application directory is not 0700.")

        # Another UID cannot pre-create the new endpoint even before the server binds it.
        deny_create = """import errno,sys
try: open(sys.argv[1], 'x').close()
except OSError as e: assert e.errno == errno.EACCES
else: raise AssertionError('Attacker created the private endpoint')
"""
        run(attacker, [sys.executable, "-c", deny_create, str(pipe)])
        lease = "OpenConquer.CrossUid." + uuid.uuid4().hex
        primary = owner(lease)
        require(pipe.stat().st_mode & 0o7777 == 0o600, "Socket is not 0600.")
        deny_socket = """import errno,os,socket,sys
for action in ('connect','unlink','bind'):
    try:
        if action == 'unlink': os.unlink(sys.argv[1])
        else:
            with socket.socket(socket.AF_UNIX) as s: getattr(s, action)(sys.argv[1])
    except OSError as e: assert e.errno == errno.EACCES, (action,e)
    else: raise AssertionError('Attacker operation succeeded: '+action)
"""
        run(attacker, [sys.executable, "-c", deny_socket, str(pipe)])
        activate(lease)
        require(line(primary) == "activated", "No activation callback.")

        primary.kill()
        primary.wait(timeout=5)
        require(os.path.lexists(pipe), "Forced termination did not leave a stale socket.")
        replacement = owner(lease)
        activate(lease)
        require(line(replacement) == "activated", "Recovered owner was not activated.")
        replacement.communicate("exit\n", timeout=10)
        require(replacement.returncode == 0, "Recovered owner failed normal shutdown.")
        require(old_socket.lstat().st_uid == attacker.pw_uid, "Product touched the legacy attacker endpoint.")

        # Metadata and no-follow checks must reject foreign objects, even when traversal is allowed.
        foreign = directory("foreign", attacker, 0o755)
        resolve(foreign, success=False)
        # All mode checks pass here; only the foreign ancestor's ownership makes it unsafe.
        protected_child = foreign / "victim-child"
        protected_child.mkdir(mode=0o700)
        os.chown(protected_child, victim.pw_uid, victim.pw_gid)
        resolve(protected_child, success=False)
        wrong_mode = directory("wide", mode=0o755)
        resolve(wrong_mode, success=False)
        require(wrong_mode.stat().st_mode & 0o777 == 0o755, "Product repaired untrusted permissions.")
        link = root / "link"
        link.symlink_to(runtime, target_is_directory=True)
        resolve(link, success=False)
        resolve("relative", success=False)
        resolve(root / ("x" * 100), success=False)
        namespace = directory("foreign-child")
        child = namespace / "openconquer"
        child.mkdir()
        os.chown(child, attacker.pw_uid, attacker.pw_gid)
        child.chmod(0o755)
        resolve(namespace, success=False)
        require(child.stat().st_uid == attacker.pw_uid, "Product modified a foreign directory.")

        # A foreign-owned endpoint planted by root is also rejected by ownership checks.
        # (The namespace normally prevents the attacker from planting it in the first place.)
        run(victim, [sys.executable, "-c",
                     "import socket,sys; s=socket.socket(socket.AF_UNIX); s.bind(sys.argv[1])",
                     str(pipe)])
        os.chown(pipe, attacker.pw_uid, attacker.pw_gid)
        failed = start(victim, [dotnet, str(probe), "listen", lease, "@current-user"])
        failed.communicate(timeout=10)
        require(failed.returncode != 0, "Foreign endpoint unexpectedly accepted.")
        require(pipe.stat().st_uid == attacker.pw_uid,
                "Foreign endpoint was modified.")
        print("PASS: cross-UID occupation, namespace isolation, activation, crash recovery, ownership, links, modes, and path limits")
    finally:
        for process in children:
            if process.poll() is None:
                process.kill()
            process.communicate(timeout=10)
        if old_created:
            # This exact socket was created by this test and was never modified by product code.
            require(old_socket.lstat().st_uid == attacker.pw_uid, "Legacy test endpoint ownership changed.")
            old_socket.unlink()
