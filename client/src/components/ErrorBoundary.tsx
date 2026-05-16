import { Component, type ErrorInfo, type ReactNode } from "react";

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Surface to console for now. A real telemetry hook would go here.
    console.error("ErrorBoundary caught", error, info.componentStack);
  }

  private reset = () => this.setState({ error: null });

  private hardReload = () => window.location.reload();

  render() {
    if (this.state.error) {
      return (
        <div className="error-fallback">
          <div className="card" style={{ maxWidth: 520 }}>
            <h2>Something went wrong</h2>
            <p className="muted">
              The page you were on threw an error. The rest of the app is fine
              — try again, or reload if it persists.
            </p>
            <pre className="error-detail">{this.state.error.message}</pre>
            <div className="row" style={{ gap: 8 }}>
              <button className="btn" onClick={this.reset}>
                Try again
              </button>
              <button className="btn secondary" onClick={this.hardReload}>
                Reload app
              </button>
            </div>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}
