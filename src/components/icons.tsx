// Ek bağımlılık olmaması için küçük inline SVG ikonlar.
type P = { className?: string };

export const SearchIcon = ({ className }: P) => (
  <svg className={className} viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8">
    <circle cx="9" cy="9" r="6" />
    <path d="m14 14 4 4" strokeLinecap="round" />
  </svg>
);

export const StarIcon = ({ className, filled }: P & { filled?: boolean }) => (
  <svg className={className} viewBox="0 0 20 20" fill={filled ? "currentColor" : "none"} stroke="currentColor" strokeWidth="1.5">
    <path
      strokeLinejoin="round"
      d="m10 2.5 2.3 4.8 5.2.7-3.8 3.7.9 5.2L10 14.4l-4.6 2.5.9-5.2-3.8-3.7 5.2-.7L10 2.5Z"
    />
  </svg>
);

export const GearIcon = ({ className }: P) => (
  <svg className={className} viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6">
    <circle cx="10" cy="10" r="2.6" />
    <path
      strokeLinejoin="round"
      d="M8.6 2.5h2.8l.4 2.1 1.5.9 2-.8 1.4 2.4-1.6 1.4v1.8l1.6 1.4-1.4 2.4-2-.8-1.5.9-.4 2.1H8.6l-.4-2.1-1.5-.9-2 .8-1.4-2.4 1.6-1.4V9.1L3.3 7.7l1.4-2.4 2 .8 1.5-.9.4-2.1Z"
    />
  </svg>
);

export const TrashIcon = ({ className }: P) => (
  <svg className={className} viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6">
    <path d="M3.5 5.5h13M8 5.5V3.8h4v1.7M5.2 5.5l.8 11h8l.8-11M8.5 8.5v5M11.5 8.5v5" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

export const XIcon =({ className }: P) => (
  <svg className={className} viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8">
    <path d="m5 5 10 10M15 5 5 15" strokeLinecap="round" />
  </svg>
);

export const BackIcon = ({ className }: P) => (
  <svg className={className} viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8">
    <path d="M12 4 6 10l6 6" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

export const CheckIcon = ({ className }: P) => (
  <svg className={className} viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2">
    <path d="m4.5 10.5 3.5 3.5 7.5-8" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);
