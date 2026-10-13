# Create your first flow

A new project starts with an empty canvas. Add the built-in script node, make it return `0`, then run the flow. This covers editing, saving, running, and viewing results.

## 1. Create a project

In the console, select **New project** and enter a recognizable name. The **Main flow** canvas appears. The project and empty canvas are saved, but an empty flow cannot run yet.

## 2. Add a script node

Find **Script** in the left **Node library** and drag it onto the main canvas. Its default code, `return 0`, returns the number `0` at runtime. Leave it unchanged for this first run.

Select the node and mark it as the **flow entry** in the right **Node inspector**. Execution starts at the entry node. A single-node flow needs no connection.

## 3. Save and run

Click **Save**. If save diagnostics report problems, fix them and save again. Then click **Run flow**.

Once accepted, **Run output** shows events and payloads; **Run console** shows the run status. A successful run has status `succeeded`. Open its record to inspect the node result.

## 4. Add another node

Drag in a second script node, connect the first node to it with a flow connection, then save and run. The nodes execute in order. Use a data connection when passing a value to a downstream parameter; a flow connection only sets execution order.

## Next steps

- [Use SereinScript](/en/script/usage) covers code editing, inputs and outputs, logs, and shared variables.
- [Projects and flows](/en/modules/projects-and-flows) explains what you created.
- [Runs and results](/en/modules/runs) explains states, events, and outputs.
- [Run a flow with the API](/en/guide/run-with-api) runs the same flow outside the console.
- [Your first node](/en/library/first-node) adds your own C# method to the node library.
