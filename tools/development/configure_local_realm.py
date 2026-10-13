#!/usr/bin/env python3
"""Explicitly opt a developer's local MSBuild invocation into a trusted realm authority."""

import argparse
import base64
import binascii
import json
import os
import sys
from pathlib import Path
from urllib.parse import urlsplit
from xml.sax.saxutils import escape

DEFAULT_AUTHORITY = Path.home() / "Library/Application Support/OpenConquer/Development/RealmDirectory/dev-v1/publication/authority.json"
LOCAL_PROPS = Path.home() / ".config/openconquer/client.local.props"
LOCAL_MARKER = "<OpenConquerLocalRealmAuthority>true</OpenConquerLocalRealmAuthority>"


def authority_is_valid(path: Path) -> None:
    if path.is_symlink() or not path.is_file():
        raise ValueError(f"Authority must be an existing regular, non-symlinked file: {path}")
    data = path.read_bytes()
    if not 1 <= len(data) <= 4096:
        raise ValueError("Authority must be between 1 and 4096 bytes.")

    def reject_duplicates(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate authority JSON property.")
            result[key] = value
        return result

    try:
        document = json.loads(data, object_pairs_hook=reject_duplicates)
    except (UnicodeDecodeError, json.JSONDecodeError) as exception:
        raise ValueError("Authority JSON is not valid UTF-8 JSON.") from exception
    if not isinstance(document, dict) or set(document) != {"catalogUri", "signatureUri", "publicKeySpki"}:
        raise ValueError("Authority must contain only catalogUri, signatureUri and publicKeySpki.")
    for field, expected in (("catalogUri", "/realms.json"), ("signatureUri", "/realms.sig")):
        value = document[field]
        if not isinstance(value, str):
            raise ValueError(f"{field} must be a string.")
        uri = urlsplit(value)
        if uri.scheme != "https" or uri.hostname != "localhost" or uri.port not in (None, 443) or uri.path != expected or uri.username or uri.password or uri.query or uri.fragment:
            raise ValueError(f"{field} must use the established loopback HTTPS endpoint.")
    try:
        public_key = base64.b64decode(document["publicKeySpki"], validate=True)
    except (TypeError, ValueError, binascii.Error) as exception:
        raise ValueError("Authority public key encoding is invalid.") from exception
    if len(public_key) not in range(64, 257):
        raise ValueError("Authority public key encoding is not in the expected SPKI size range.")


def configuration_text(authority: Path) -> str:
    path = escape(str(authority), {'"': '&quot;', "'": '&apos;'})
    return ("<Project>\n  <PropertyGroup>\n"
            f"    <OpenConquerCatalogAuthorityFile>{path}</OpenConquerCatalogAuthorityFile>\n"
            f"    {LOCAL_MARKER}\n"
            "  </PropertyGroup>\n</Project>\n")


def configure(authority: Path, destination: Path) -> str:
    authority = authority.expanduser().absolute()
    authority_is_valid(authority)
    expected = configuration_text(authority).encode("utf-8")
    if destination.is_symlink():
        raise ValueError("Local MSBuild properties must not be a symbolic link.")
    if destination.exists():
        if not destination.is_file() or destination.read_bytes() != expected:
            raise ValueError(f"Different local MSBuild properties already exist; not overwriting: {destination}")
        return "Already configured"
    parent = destination.parent
    if parent.is_symlink():
        raise ValueError("Local MSBuild configuration directory must not be a symbolic link.")
    parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    if not parent.is_dir():
        raise ValueError("Local MSBuild configuration directory is not a directory.")
    fd = os.open(destination, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(expected)
            stream.flush()
            os.fsync(stream.fileno())
    except BaseException:
        destination.unlink(missing_ok=True)
        raise
    return "Configured"


def disable(destination: Path) -> str:
    if not destination.exists() and not destination.is_symlink():
        return "Already disabled"
    if destination.is_symlink() or not destination.is_file():
        raise ValueError("Refusing to delete a linked or non-file configuration.")
    data = destination.read_bytes()
    if LOCAL_MARKER.encode("utf-8") not in data or not data.startswith(b"<Project>\n"):
        raise ValueError("Refusing to remove unrecognized local MSBuild properties.")
    destination.unlink()
    return "Disabled"


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--authority", type=Path, default=DEFAULT_AUTHORITY, help="Existing public authority.json (not a private key)")
    parser.add_argument("--disable", action="store_true", help="Remove only a recognizable local configuration")
    args = parser.parse_args(argv)
    try:
        message = disable(LOCAL_PROPS) if args.disable else configure(args.authority, LOCAL_PROPS)
    except (ValueError, OSError, PermissionError) as exception:
        print(f"OpenConquer local realm: {exception}", file=sys.stderr)
        return 1
    print(f"{message}: {LOCAL_PROPS}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
