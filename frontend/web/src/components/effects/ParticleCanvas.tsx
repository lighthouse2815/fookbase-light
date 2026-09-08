import { useEffect, useRef } from 'react'

interface Particle {
  x: number
  y: number
  vx: number
  vy: number
  size: number
  opacity: number
  pulse: number
}

export default function ParticleCanvas() {
  const canvasRef = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return

    let animId: number
    const particles: Particle[] = []
    const PARTICLE_COUNT = 60
    const MAX_DIST = 130
    const CYAN = '0, 255, 255'
    const GREEN = '0, 255, 65'

    const resize = () => {
      canvas.width = canvas.offsetWidth
      canvas.height = canvas.offsetHeight
    }

    const spawnParticles = () => {
      particles.length = 0
      for (let i = 0; i < PARTICLE_COUNT; i++) {
        particles.push({
          x: Math.random() * canvas.width,
          y: Math.random() * canvas.height,
          vx: (Math.random() - 0.5) * 0.4,
          vy: (Math.random() - 0.5) * 0.4,
          size: Math.random() * 1.5 + 0.5,
          opacity: Math.random() * 0.5 + 0.2,
          pulse: Math.random() * Math.PI * 2,
        })
      }
    }

    const draw = () => {
      ctx.clearRect(0, 0, canvas.width, canvas.height)

      for (const p of particles) {
        // Move
        p.x += p.vx
        p.y += p.vy
        p.pulse += 0.02

        // Wrap
        if (p.x < 0) p.x = canvas.width
        if (p.x > canvas.width) p.x = 0
        if (p.y < 0) p.y = canvas.height
        if (p.y > canvas.height) p.y = 0

        // Pulsing opacity
        const alpha = p.opacity * (0.7 + 0.3 * Math.sin(p.pulse))
        const color = p.size > 1.2 ? CYAN : GREEN

        // Draw particle dot
        ctx.beginPath()
        ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2)
        ctx.fillStyle = `rgba(${color}, ${alpha})`
        ctx.fill()

        // Draw connections
        for (const other of particles) {
          const dx = p.x - other.x
          const dy = p.y - other.y
          const dist = Math.sqrt(dx * dx + dy * dy)
          if (dist < MAX_DIST && dist > 0) {
            const lineAlpha = (1 - dist / MAX_DIST) * 0.2 * alpha
            ctx.beginPath()
            ctx.moveTo(p.x, p.y)
            ctx.lineTo(other.x, other.y)
            ctx.strokeStyle = `rgba(${CYAN}, ${lineAlpha})`
            ctx.lineWidth = 0.5
            ctx.stroke()
          }
        }
      }

      animId = requestAnimationFrame(draw)
    }

    resize()
    spawnParticles()
    draw()

    const ro = new ResizeObserver(() => {
      resize()
      spawnParticles()
    })
    ro.observe(canvas)

    return () => {
      cancelAnimationFrame(animId)
      ro.disconnect()
    }
  }, [])

  return (
    <canvas
      ref={canvasRef}
      style={{
        position: 'fixed',
        inset: 0,
        width: '100%',
        height: '100%',
        pointerEvents: 'none',
        zIndex: 0,
        opacity: 0.6,
      }}
    />
  )
}
