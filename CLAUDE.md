# IPLab – Project Notes for Claude

## Project Goal

IPLab is a C# desktop tool for visually building, tuning, and debugging OpenCV image-processing pipelines.

The tool is intended for engineers who want to experiment with image-processing flows visually, inspect intermediate results, tune parameters, and then export the result for production use.

A concise product definition:

> IPLab is an offline C# desktop tool for visually building, tuning, and debugging OpenCV image-processing pipelines on saved images, with live previews of each stage and export as C# code or JSON pipeline definitions.

## Current Scope

The first version should focus on **offline image processing** only.

This means:

- No live camera support for the MVP.
- No video streaming requirements.
- No real-time frame-rate constraints.
- The user loads saved image files and builds processing flows around them.

This keeps the system simpler, more reproducible, and easier to debug.

## Main Workflow

Basic intended workflow:

1. Open an image or image set.
2. Build an image-processing flow visually.
3. Add processing steps, called **operators**.
4. Connect operators together.
5. Run the full flow or individual operators.
6. Inspect the output of each operator.
7. Tune parameters.
8. Export the flow as JSON flow definition plus a reader/executor module.



## Terminology

### Operator

An **operator** is the generic name for one processing step in the flow.

Examples:

- Load image
- Blur
- Threshold
- Morphology
- Find contours
- Detect blobs
- Measure features
- Draw overlay

Each operator should have inputs, outputs, parameters, execution status, and some displayable result.

### FlowVis

A **flow** is the complete visual image-processing pipeline.

It describes:

- Which operators exist.
- How they are connected.
- What parameters each operator uses.
- Enough information to save, load, and execute the pipeline.

The exact flow data structure is still undecided.

### FlowEx

`FlowEx` is the proposed name for the software module responsible for executing flows.

Expected responsibilities:

- Execute the entire flow.
- Execute a selected operator individually.
- Resolve operator dependencies.
- Store intermediate runtime results.
- Store execution status for each operator.
- Report errors and warnings.
- Provide results to the UI.

Important idea:

The saved flow should probably be lightweight and serializable, while `FlowEx` should manage heavy runtime data such as OpenCV `Mat` objects, contours, keypoints, measurements, and intermediate results.

## UI Concept

The UI should be a desktop C# application.

The current preferred layout is:

```text
┌──────────────────────────────────────────────┐
│ Ribbon                                       │
│ File | Run | View | etc.                     │
├───────────────────────┬──────────────────────┤
│ Left: Pipeline Editor │ Right: Inspector     │
│                       │                      │
│ Operator library      │ Tabs:                │
│ Flow diagram          │  - Image Preview     │
│ Connections           │  - Textualt Result   │
│                       │                      │
├───────────────────────┴──────────────────────┤
│ Bottom Status Bar                            │
└──────────────────────────────────────────────┘
```

## Ribbon

The ribbon should include important actions such as:

- Open image
- Open project
- Save
- Save as
- Run all
- Stop
- Clear results
- View modifiers
- Settings

Possible ribbon groups:

```text
File     Open Project | Save | Save As
Run      Run All | Run Selected | Stop | Clear Results
View     Zoom Fit | Actual Size | Show Grid | Show Overlays
Tools    Settings | Operator Manager
```

## Main Screen Areas

### Left Side – Pipeline Editor

The left side contains the visual flow editor.

It should include:

- The flow diagram layout.
- Operator nodes.
- Connections between operators.
- A panel or library for adding new operators (with textual search)

### Right Side – Inspector

The right side shows information about the currently selected operator.

It should use a tab view.

Planned tabs:

1. **Image Preview**  
   Shows the output image of the selected operator.

2. **Text / Data View**  
   Shows textual output, debug information, measurements, contours count, errors, metadata, etc.

The term **Inspector** is preferred over “preview side” because it displays more than just an image.

## Bottom Status Bar

The status bar should be at the bottom of the screen.

Possible contents:

```text
Ready | Image: sample.tif | Last run: 34 ms | Errors: 0
```

Useful information could include:

- Current image name
- Pipeline state
- Last execution time
- Number of errors
- General status: Ready / Running / Failed

## Operator Visual Status

Each operator node should visually indicate its execution status.

Suggested states:

```text
Not Run      neutral / gray
Running      blue or animated border
Success      green
Failed       red
Disabled     faded gray
```

# Execution Requirements

The execution system should support:

- Running the full flow.
- Running a selected operator.
- Saving intermediate outputs in runtime memory.
- Displaying intermediate images and features.
- Reporting operator status.
- Handling failed operators cleanly.

The user should be able to click any operator and inspect its output.

## Architecture Direction

The exact flow data structure is not yet decided.

However, the general direction is:

- There will be a serializable flow definition.
- There will be a runtime executor called `FlowEx`.
- Each OpenCV operation will likely have a corresponding operator implementation class.
- Operators will probably share a common interface or base abstraction.
- The flow must describe the operators and their connections.
- Runtime intermediate memory should probably live in the executor, not directly in the saved flow file.

Important caution:

Avoid committing too early to a specific serializable class hierarchy. The saved model should remain simple, stable, and versionable.

## Export Options

Two export directions are considered:

### JSON + Executor

The tool exports a JSON flow file.

A production app can load the JSON and execute it using a compatible runtime executor.

Advantages:

- Simpler than code generation.
- Easier to version.
- Easier to debug.
- Allows the same flow to be reused by the tool and production code.



## Open Questions

Important unresolved decisions:

1. Should the visual flow be a free node graph or a simpler vertical step list? Node Graph for multiple paths, including parallel execution.
2. What is the exact flow data model?
3. What should an operator interface look like? It should include 
   1. an ID (ex. O21, O for Operator)
   2. the Type (ex. Line Detection or an representative Icon)
   3. a user selected name (ex. "LeftWaferEdge")
4. How should multiple outputs be represented? Eigther on the image or on the results textual display
5. How should non-image data be passed between operators? strongly types, but stored at the memory as an generic Object, which could be a int/bool/double/array and so on.
6. How should parameters be described for automatic UI generation? the operator interface would teach the UI how to dislay it. BTW TBD where the settings window would open once double click an operator.
7. Should operators be built-in only, or should there be a plugin system? what does this mean?
8. Should the executor support partial recomputation when only one parameter changes? redo everything
9. How should large images and memory-heavy intermediate results be managed? no special treatment for now
10. What is the minimum useful MVP operator set? load image + median blur + edge detection + expose results

## MVP Recommendation

A good MVP could include:

- Open image
- Add operators visually
- Connect simple linear flow
- Run all
- Preview output image per operator
- Show textual result per operator
- Edit parameters
- Show operator status colors
- Save/load flow as JSON
- Execute saved flow through `FlowEx`

Initial operators:

- Input image
- Convert color to BW or split channel
- Blur
- Threshold
- Morphology
- Find contours
- Line detection
- Draw contours / overlay

The MVP should avoid complex graph features until the basic linear pipeline works well.
