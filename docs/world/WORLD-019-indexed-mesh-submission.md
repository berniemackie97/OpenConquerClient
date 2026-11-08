# WORLD-019: Indexed mesh submission

## Evidence and scope

Indexed mesh submission, vertex layout and draw states are documented in `20_notes/Rendering/c3_core_dll/05_phy.md` (GPU output). OpenGL depth conversion, VAO lifetime and state cleanup are application decisions. The synthetic framebuffer test does not establish appearance equivalence for arbitrary character scenes.

The rendering backend accepts skinned vertex bytes in a single specified 36-byte
layout: position float3 at byte 0, normal float3 at 12, packed ARGB at 24,
and UV float2 at 28. Triangle indices remain 16-bit, are validated at mesh
creation, and are uploaded once. Vertex buffers are allocated once and updated
through the caller's existing output span as each animation frame is sampled.

The GPU mesh owns its VAO, dynamic vertex buffer and immutable triangle-index
buffer. The existing OpenGL render target provides a 16-bit depth attachment.
The renderer uses that target within its normal BeginFrame/EndFrame lifetime.

## Draw state

- Triangle list and unsigned 16-bit indices
- Row-vector composite clip matrix; input depth range 0–1 converts to GL -1–1
- Vertex diffuse is unpacked from little-endian ARGB as BGRA attributes
- Unlit stage-0 texture multiplied by per-vertex diffuse, with UVs unchanged
- Alpha threshold: discard when the final alpha is below 8/255
- Source-alpha / inverse-source-alpha blending
- Depth comparison less-than-or-equal with depth writes enabled
- Front-face culling for the clockwise or counterclockwise source selector;
  double-sided meshes disable face culling
- Repeat texture addressing with linear minification and magnification

The draw path restores the prior 2D pipeline's usable state after drawing:
depth test and face culling disabled, sampler and texture unbound, blend
disabled, and depth writes enabled. Each indexed draw sets its own depth,
blend, cull, and texture state before submission.

The OpenGL conformance runner uploads a three-vertex mesh, checks a colored
center pixel, verifies that a farther triangle cannot overwrite it, verifies
face culling and double-sided drawing, checks the alpha cutoff, and draws a
sprite afterward to catch state leakage. It runs through the real renderer
and reads the render target.

## Integration boundary

The graphics backend deliberately does not reference the content decoder.
A caller submits the existing `C3PhyOutputVertex` span as bytes without
converting it to another vertex structure.

This slice does not resolve a character part, assign a mesh texture, choose a
motion, build a world camera, submit a shadow pass, or place a role among
scenery. The world scene must supply those inputs; passing this GPU draw
conformance test is not a claim that the local hero is already visible.
