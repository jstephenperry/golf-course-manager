import { type ReactNode, useEffect } from "react";

interface Props {
  title: string;
  onClose: () => void;
  onSubmit?: () => void;
  submitLabel?: string;
  children: ReactNode;
  hideFooter?: boolean;
}

export function Modal({
  title,
  onClose,
  onSubmit,
  submitLabel = "Save",
  children,
  hideFooter,
}: Props) {
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [onClose]);

  return (
    <div className="modal-overlay" onMouseDown={onClose}>
      <div className="modal" onMouseDown={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button className="btn ghost" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>
        <div className="modal-body">{children}</div>
        {!hideFooter && (
          <div className="modal-footer">
            <button className="btn secondary" onClick={onClose}>
              Cancel
            </button>
            {onSubmit && (
              <button className="btn" onClick={onSubmit}>
                {submitLabel}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
