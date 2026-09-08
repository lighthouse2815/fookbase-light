interface GlitchTextProps {
  text: string
  className?: string
  tag?: 'h1' | 'h2' | 'h3' | 'span' | 'div' | 'p'
  style?: React.CSSProperties
}

export default function GlitchText({
  text,
  className = '',
  tag: Tag = 'span',
  style,
}: GlitchTextProps) {
  return (
    <Tag
      className={`glitch-text ${className}`}
      data-text={text}
      style={style}
    >
      {text}
    </Tag>
  )
}
