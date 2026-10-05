import { useEffect, useId, useRef, useState } from 'react'
import './animatedDeleteButton.css'

interface AnimatedDeleteButtonProps {
  disabled?: boolean
  onDelete: () => Promise<boolean>
  onDeleted: () => void
  onBusyChange: (busy: boolean) => void
}

export default function AnimatedDeleteButton({ disabled = false, onDelete, onDeleted, onBusyChange }: AnimatedDeleteButtonProps) {
  const [state, setState] = useState<'idle' | 'eat' | 'pending' | 'done' | 'error'>('idle')
  const iconRef = useRef<SVGSVGElement>(null)
  const labelRef = useRef<HTMLSpanElement>(null)
  const animationsRef = useRef<Animation[]>([])
  const busyRef = useRef(false)
  const mountedRef = useRef(false)
  const clipId = useId()

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      animationsRef.current.forEach((animation) => animation.cancel())
    }
  }, [])

  const remove = async () => {
    if (disabled || busyRef.current) return
    busyRef.current = true
    onBusyChange(true)
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    try {
      setState('eat')
      // Send the real request immediately; the animation never decides its outcome.
      const request = Promise.resolve().then(onDelete).catch(() => false)
      const icon = iconRef.current?.getBoundingClientRect()
      const letters = Array.from(labelRef.current?.children ?? []) as HTMLElement[]
      const animations = !reducedMotion && icon ? letters.map((letter, index) => {
        const bounds = letter.getBoundingClientRect()
        const dx = icon.left + icon.width / 2 - bounds.left - bounds.width / 2
        const dy = icon.top + icon.height / 3 - bounds.top - bounds.height / 2
        const rotation = index % 2 ? 35 : -35
        return letter.animate([
          { transform: 'translate(0, 0) scale(1)', opacity: 1 },
          { transform: `translate(${dx * .5}px, ${dy - 10}px) rotate(${rotation}deg) scale(.8)`, opacity: 1, offset: .55 },
          { transform: `translate(${dx}px, ${dy}px) rotate(${rotation * 2}deg) scale(.2)`, opacity: 0 },
        ], { duration: 320, delay: index * 60, fill: 'forwards', easing: 'cubic-bezier(.3, 0, .5, 1)' })
      }) : []
      animationsRef.current = animations
      await Promise.all(animations.map((animation) => animation.finished.catch(() => undefined)))
      if (!mountedRef.current) return
      setState('pending')
      const removed = await request
      if (!mountedRef.current) return
      if (removed) {
        setState('done')
        if (!reducedMotion) await new Promise((resolve) => window.setTimeout(resolve, 240))
        if (mountedRef.current) onDeleted()
      } else {
        setState('error')
        animations.forEach((animation) => animation.reverse())
        await Promise.all(animations.map((animation) => animation.finished.catch(() => undefined)))
      }
    } finally {
      animationsRef.current.forEach((animation) => animation.cancel())
      animationsRef.current = []
      busyRef.current = false
      if (mountedRef.current) {
        setState('idle')
        onBusyChange(false)
      }
    }
  }

  return <button type="button" className="animated-delete" data-state={state} disabled={disabled || state !== 'idle'} aria-label="Xóa" aria-busy={state !== 'idle'} onClick={() => void remove()}>
    <svg ref={iconRef} className="animated-delete__icon" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
      <defs><clipPath id={clipId}><path d="M6 8h12l-1 12H7Z" /></clipPath></defs>
      <g className="animated-delete__can">
        <g clipPath={`url(#${clipId})`}><path className="animated-delete__fill" d="M5 10q3-2 6 0t7 0v12H5Z" fill="currentColor" stroke="none" /></g>
        <path d="m6 8 1 12h10l1-12M10 11v6m4-6v6" />
        <g className="animated-delete__lid"><path d="M4 5h16M9 5V3h6v2" /></g>
      </g>
    </svg>
    <span ref={labelRef} className="animated-delete__label" aria-hidden="true">{[...'Xóa'].map((letter, index) => <span key={index}>{letter}</span>)}</span>
    <svg className="animated-delete__spinner" viewBox="0 0 32 32" aria-hidden="true"><circle cx="16" cy="16" r="14" /></svg>
    <svg className="animated-delete__check" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="m5 12 4 4L19 6" /></svg>
    <span className="sr-only" role="status">{state === 'eat' || state === 'pending' ? 'Đang xóa…' : state === 'done' ? 'Đã xóa.' : state === 'error' ? 'Không thể xóa. Vui lòng thử lại.' : ''}</span>
  </button>
}
