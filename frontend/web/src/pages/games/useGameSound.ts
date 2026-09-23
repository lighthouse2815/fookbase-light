import { useCallback, useEffect, useRef, useState } from 'react'

const soundKey = 'fookbase.games.sound'

export function useGameSound() {
  const [enabled, setEnabled] = useState(() => {
    try { return localStorage.getItem(soundKey) !== 'off' } catch { return true }
  })
  const enabledRef = useRef(enabled)
  const audioRef = useRef<AudioContext | null>(null)
  const voicesRef = useRef(new Set<OscillatorNode>())

  const stop = useCallback(() => {
    for (const voice of voicesRef.current) voice.stop()
    voicesRef.current.clear()
  }, [])

  const play = useCallback((frequency: number, duration = 0.22, type: OscillatorType = 'sine') => {
    if (!enabledRef.current || typeof AudioContext === 'undefined') return
    try {
      const audio = audioRef.current ??= new AudioContext()
      if (audio.state === 'suspended') void audio.resume().catch(() => undefined)
      if (audio.state === 'closed' || voicesRef.current.size >= 12) return
      const voice = audio.createOscillator()
      const gain = audio.createGain()
      const now = audio.currentTime
      voice.type = type
      voice.frequency.setValueAtTime(frequency, now)
      gain.gain.setValueAtTime(0, now)
      gain.gain.linearRampToValueAtTime(0.1, now + 0.015)
      gain.gain.exponentialRampToValueAtTime(0.001, now + duration)
      voice.connect(gain).connect(audio.destination)
      voicesRef.current.add(voice)
      voice.onended = () => { voicesRef.current.delete(voice); voice.disconnect(); gain.disconnect() }
      voice.start(now)
      voice.stop(now + duration + 0.02)
    } catch { /* Sound is optional when a browser cannot create an audio context. */ }
  }, [])

  const toggle = () => {
    const next = !enabledRef.current
    enabledRef.current = next
    setEnabled(next)
    if (!next) stop()
    else play(523.25, 0.12)
    try { localStorage.setItem(soundKey, next ? 'on' : 'off') } catch { /* Storage may be disabled. */ }
  }

  useEffect(() => () => {
    stop()
    if (audioRef.current) void audioRef.current.close().catch(() => undefined)
    audioRef.current = null
  }, [stop])

  return { enabled, toggle, play, stop }
}
