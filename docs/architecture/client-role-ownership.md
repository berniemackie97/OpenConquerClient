# Role-creation ownership — CP2.1b

After CP2.1a introduced `ClientWorld`, the host still held the role screen, its
cached ini/text/font/raster assets, presentation and return/close state.
`ClientRoleCreation` now owns those fields and role input routing. The shared
content source is named `_content`; it also serves login and world loading.

The screen still interprets native Flash commands and drives `RoleCreationExchange`.
Its implementation changes only to implement the internal screen boundary; the
admission method becomes public on the internal type. The exchange, protocol,
Flash scripts and world implementation are unchanged.

## Transitions and resources

- Opening transfers the accepted session into a constructed screen. Expected
  content failures start unclaimed-session cleanup and report failure to the login
  coordinator; unexpected failures start the same cleanup and propagate.
- The frame loop observes completed role cleanup before allowing another screen.
  Each update retains Tick, world-admission callback, screen close, then requested
  return ordering. Return reports the connection-loss flag before starting close.
- A successful admission starts loading and creates the world before the role
  screen is dropped. The existing exchange transfers its session exactly once;
  disposing the role screen then leaves the admitted world session usable.
- Role presentation is registered in the window's existing `ResourceOwnership`
  group between login presentation and world textures. The owner detaches caret
  callbacks before that group releases graphics in reverse order. Cached name-gate
  fonts are released after the window closes, at the existing host release point.
- Shutdown waits for the active screen and pending close, clearing both even when
  cleanup fails. Repeated owner disposal awaits the same task.

The narrow `IRoleCreationScreen` boundary lets lifetime tests control screen close
and outcomes without constructing a GPU context or an authenticated socket for
each failure case. Production always constructs `RoleCreationScreen`.

## Evidence and validation

This is a modern ownership refactor. Existing native behavior is documented in
`20_notes/Architecture/12_create_role_flow.md` and
`20_notes/Architecture/11_login_ui_assets_and_interactions.md`. Existing role
command, name-validation and handshake/creation fixtures remain the authority for
their respective contracts. No native symbol change or BNDB save is involved.

Owner tests cover return versus connection loss, reopening after observed close,
deferred return, initialization failure, cleanup failure, repeated disposal, pointer
release outside the stage and caret detachment. A transport integration test reuses
the pinned creation/proof/world-entry fixtures, then sends and receives a walk on
the same admitted session after role-owner disposal. This verifies lifetime and
transport behavior; it is not a running-client or native-framebuffer comparison.

Local validation on SDK 10.0.403: locked restore, zero-warning Release build,
format verification and 2,798 focused tests pass (Client 1,463, Content 750,
Rendering 499, Platform 86). Eleven client cases are new, including the fixture-backed
handoff test. Existing world, exchange, Flash scripts and networking source files
are unchanged from merged CP2.1a.

CP2.1c remains the separate login owner, explicit application-state and linear
disposal slice. Clock/logging changes and the broader documentation refresh retain
their existing later checkpoints.
