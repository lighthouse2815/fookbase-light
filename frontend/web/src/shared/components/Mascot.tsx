import { Mascot as PageMascot } from 'page-mascot'

type MascotSize = 'sm' | 'md' | 'lg'

interface MascotProps {
  size?: MascotSize
  animated?: boolean
  className?: string
}

const SIZE_PX: Record<MascotSize, number> = {
  sm: 48,
  md: 72,
  lg: 104,
}

/**
 * Wrapper around `page-mascot` <Mascot>.
 * Keeps the same simple API (size / animated / className) used across all pages.
 * The mascot follows the cursor and blinks when poked.
 */
export default function Mascot({ size = 'md', animated: _animated, className }: MascotProps) {
  return (
    <PageMascot
      directions="/mascots/fox-directions.webp"
      reactions="/mascots/fox-reactions.webp"
      size={SIZE_PX[size]}
      className={className}
      label="Fooky mascot"
    />
  )
}
