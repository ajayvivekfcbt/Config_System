import React from "react";
import "./SplitPaneLayout.css";

interface SplitPaneLayoutProps {
  left: React.ReactNode;
  right: React.ReactNode;
  leftWidth?: number; // percentage (default: 30)
  onLeftWidthChange?: (width: number) => void;
}

export default function SplitPaneLayout({
  left,
  right,
  leftWidth = 30,
  onLeftWidthChange,
}: SplitPaneLayoutProps) {
  const [width, setWidth] = React.useState(leftWidth);
  const [isDragging, setIsDragging] = React.useState(false);
  const containerRef = React.useRef<HTMLDivElement>(null);

  const handleMouseDown = () => {
    setIsDragging(true);
  };

  const handleMouseUp = () => {
    setIsDragging(false);
  };

  const handleMouseMove = (e: MouseEvent) => {
    if (!isDragging || !containerRef.current) return;

    const container = containerRef.current;
    const rect = container.getBoundingClientRect();
    const newWidth = ((e.clientX - rect.left) / rect.width) * 100;

    // Clamp between 15% and 85%
    const clampedWidth = Math.max(15, Math.min(85, newWidth));
    setWidth(clampedWidth);
    onLeftWidthChange?.(clampedWidth);
  };

  React.useEffect(() => {
    if (isDragging) {
      document.addEventListener("mousemove", handleMouseMove);
      document.addEventListener("mouseup", handleMouseUp);

      return () => {
        document.removeEventListener("mousemove", handleMouseMove);
        document.removeEventListener("mouseup", handleMouseUp);
      };
    }
  }, [isDragging]);

  return (
    <div
      ref={containerRef}
      className="split-pane-layout"
      style={
        {
          "--left-width": `${width}%`,
          "--right-width": `${100 - width}%`,
        } as React.CSSProperties
      }
    >
      <div className="split-pane-left">{left}</div>
      <div
        className={`split-pane-divider ${isDragging ? "dragging" : ""}`}
        onMouseDown={handleMouseDown}
      />
      <div className="split-pane-right">{right}</div>
    </div>
  );
}
