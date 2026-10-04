import { useEffect, useLayoutEffect, useRef } from 'react'

const handledKeys = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ShiftLeft', 'ShiftRight', 'Space', 'KeyE'])

export function useKeyboard(active: boolean) {
  const keys = useRef(new Set<string>())
  const actions = useRef({ jump: false, interact: false })
  const isActive = useRef(active)

  useLayoutEffect(() => {
    isActive.current = active
    if (!active) { keys.current.clear(); actions.current.jump = false; actions.current.interact = false }
  }, [active])

  useEffect(() => {
    const clear = () => { keys.current.clear(); actions.current.jump = false; actions.current.interact = false }
    const down = (event: KeyboardEvent) => {
      if (!isActive.current || !handledKeys.has(event.code)) return
      const target = event.target
      if (target instanceof HTMLElement && (target.isContentEditable || /INPUT|TEXTAREA|SELECT/.test(target.tagName))) return
      event.preventDefault()
      keys.current.add(event.code)
      if (!event.repeat && event.code === 'Space') actions.current.jump = true
      if (!event.repeat && event.code === 'KeyE') actions.current.interact = true
    }
    const up = (event: KeyboardEvent) => { keys.current.delete(event.code) }
    window.addEventListener('keydown', down)
    window.addEventListener('keyup', up)
    window.addEventListener('blur', clear)
    document.addEventListener('visibilitychange', clear)
    return () => {
      clear()
      window.removeEventListener('keydown', down)
      window.removeEventListener('keyup', up)
      window.removeEventListener('blur', clear)
      document.removeEventListener('visibilitychange', clear)
    }
  }, [])

  return { keys, actions }
}

export type KeyboardState = ReturnType<typeof useKeyboard>
