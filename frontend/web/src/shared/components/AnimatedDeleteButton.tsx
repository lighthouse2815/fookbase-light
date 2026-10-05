import { useEffect, useId, useRef, useState } from 'react'
import { useSoundEffects } from '../useSoundEffects'
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
  const { enabled: soundEnabled, toggle: toggleSound, play } = useSoundEffects('fookbase.delete.sound')

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
      play(180, .12, 'sine', .08)
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
      await Promise.all(animations.map((animation, index) => animation.finished.then(() => {
        if (mountedRef.current) play(261.6 * Math.pow(2, [0, 2, 4][index] / 12), .16, 'triangle', .09)
      }).catch(() => undefined)))
      if (!mountedRef.current) return
      setState('pending')
      play(520, .08, 'sine', .05)
      const removed = await request
      if (!mountedRef.current) return
      if (removed) {
        setState('done')
        play(523.25, .2, 'sine', .08)
        play(659.25, .12, 'sine', .07, .06)
        if (!reducedMotion || soundEnabled) await new Promise((resolve) => window.setTimeout(resolve, 240))
        if (mountedRef.current) onDeleted()
      } else {
        setState('error')
        play(150, .25, 'sawtooth', .04)
        play(110, .3, 'sawtooth', .04, .12)
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

  return <>
    <button type="button" disabled={state !== 'idle'} onClick={toggleSound} aria-pressed={soundEnabled} aria-label={soundEnabled ? 'Tắt âm thanh hiệu ứng' : 'Bật âm thanh hiệu ứng'} title={soundEnabled ? 'Tắt âm thanh hiệu ứng' : 'Bật âm thanh hiệu ứng'} className="grid h-9 w-9 shrink-0 place-items-center rounded-lg border-0 bg-transparent text-text-muted hover:bg-surface-2 hover:text-text focus-visible:outline-2 focus-visible:outline-primary disabled:opacity-50">
      <svg viewBox="0 0 24 24" aria-hidden="true" className="h-[18px] w-[18px] fill-none stroke-current" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
        <path d="M11 5 6 9H3v6h3l5 4Z" />
        {soundEnabled ? <path d="M15 8a6 6 0 0 1 0 8m3-11a10 10 0 0 1 0 14" /> : <path d="m16 9 5 6m0-6-5 6" />}
      </svg>
    </button>
    <button type="button" className="animated-delete" data-state={state} disabled={disabled || state !== 'idle'} aria-label="Xóa" aria-busy={state !== 'idle'} onClick={() => void remove()}>
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
  </>
}
