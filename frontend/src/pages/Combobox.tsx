import { useEffect, useRef, useState } from "react";

type Props = {
  value: string;
  onChange: (value: string) => void;
  options: string[];
  placeholder?: string;
  className?: string;
  required?: boolean;
};

export default function Combobox({ value, onChange, options, placeholder, className, required }: Props) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const wrapRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const onDocClick = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, []);

  // When a value is already selected (query is empty), show ALL options.
  // While the user is actively typing, filter by the typed text.
  const q = query.trim().toLowerCase();
  const shown = q ? options.filter((o) => o.toLowerCase().includes(q)) : options;

  const select = (o: string) => {
    onChange(o);
    setQuery("");
    setOpen(false);
  };

  return (
    <div className="combobox" ref={wrapRef}>
      <input
        className={className}
        placeholder={placeholder}
        value={open ? query : value}
        required={required && !value ? true : undefined}
        onFocus={() => {
          setQuery("");
          setOpen(true);
        }}
        onClick={() => setOpen(true)}
        onChange={(e) => {
          setQuery(e.target.value);
          onChange(e.target.value);
          setOpen(true);
        }}
      />
      {open && shown.length > 0 && (
        <ul className="combobox-list">
          {shown.map((o) => (
            <li
              key={o}
              className={o === value ? "active" : ""}
              onMouseDown={(e) => {
                e.preventDefault();
                select(o);
              }}
            >
              {o}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
