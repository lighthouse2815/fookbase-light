type MascotSize = 'sm' | 'md' | 'lg'

interface MascotProps {
  size?: MascotSize
  animated?: boolean
  className?: string
}

const SIZE_MAP: Record<MascotSize, number> = {
  sm: 40,
  md: 56,
  lg: 80,
}

/**
 * Fooky — the Fookbase mascot.
 * Props:
 *   size     – 'sm' | 'md' (default) | 'lg'
 *   animated – adds a gentle floating animation
 *   className – extra Tailwind / CSS classes
 */
export default function Mascot({ size = 'md', animated = false, className = '' }: MascotProps) {
  const px = SIZE_MAP[size]

  return (
    <span
      className={`inline-flex items-center justify-center shrink-0 ${animated ? 'animate-[mascot-float_3s_ease-in-out_infinite]' : ''} ${className}`}
      aria-hidden="true"
      style={{ width: px, height: px }}
    >
      <svg
        viewBox="0 0 80 80"
        width={px}
        height={px}
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
      >
        {/* Body */}
        <rect x="12" y="28" width="56" height="42" rx="14" fill="#2374e1" />
        {/* Head */}
        <rect x="20" y="8" width="40" height="34" rx="12" fill="#4a93e8" />
        {/* Antenna */}
        <line x1="40" y1="8" x2="40" y2="1" stroke="#e4e6eb" strokeWidth="2.5" strokeLinecap="round" />
        <circle cx="40" cy="0" r="3" fill="#e7a33e" />
        {/* Eyes */}
        <circle cx="30" cy="24" r="5" fill="white" />
        <circle cx="50" cy="24" r="5" fill="white" />
        <circle cx="31" cy="25" r="2.5" fill="#18191a" />
        <circle cx="51" cy="25" r="2.5" fill="#18191a" />
        {/* Eye shine */}
        <circle cx="32" cy="23.5" r="1" fill="white" />
        <circle cx="52" cy="23.5" r="1" fill="white" />
        {/* Smile */}
        <path d="M32 34 Q40 40 48 34" stroke="white" strokeWidth="2.5" strokeLinecap="round" fill="none" />
        {/* Body screen */}
        <rect x="26" y="40" width="28" height="18" rx="5" fill="#1a5bbf" />
        <circle cx="34" cy="49" r="3" fill="#41b35d" opacity="0.9" />
        <circle cx="46" cy="49" r="3" fill="#e7a33e" opacity="0.9" />
        {/* Feet */}
        <rect x="22" y="66" width="14" height="8" rx="4" fill="#1a5bbf" />
        <rect x="44" y="66" width="14" height="8" rx="4" fill="#1a5bbf" />
      </svg>
      <style>{`
        @keyframes mascot-float {
          0%, 100% { transform: translateY(0); }
          50%       { transform: translateY(-6px); }
        }
      `}</style>
    </span>
  )
}

