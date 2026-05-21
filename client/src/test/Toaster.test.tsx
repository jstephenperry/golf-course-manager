import { describe, expect, it } from "vitest";
import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ToasterProvider, useToaster } from "../components/Toaster";

function Harness() {
  const t = useToaster();
  return (
    <button onClick={() => t.push({ kind: "success", message: "Saved" })}>
      Fire
    </button>
  );
}

describe("Toaster", () => {
  it("shows then auto-dismisses a toast", async () => {
    const user = userEvent.setup();
    render(
      <ToasterProvider>
        <Harness />
      </ToasterProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Fire" }));
    expect(screen.getByText("Saved")).toBeInTheDocument();

    await act(async () => {
      await new Promise((r) => setTimeout(r, 3700));
    });
    expect(screen.queryByText("Saved")).not.toBeInTheDocument();
  }, 8000);

  it("manual dismiss removes the toast", async () => {
    const user = userEvent.setup();
    render(
      <ToasterProvider>
        <Harness />
      </ToasterProvider>,
    );
    await user.click(screen.getByRole("button", { name: "Fire" }));
    expect(screen.getByText("Saved")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Dismiss" }));
    expect(screen.queryByText("Saved")).not.toBeInTheDocument();
  });
});
