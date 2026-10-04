import { useCallback, useEffect, useRef, useState, type RefObject } from 'react'
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { apiBaseUrl } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { getAuthSession } from '../../auth/session'
import { createJumping, startJumping, type JumpingState } from './jumpingEngine'

export interface JumpingRound { id: string; seed: number; startsAtUtc: string }
interface Membership { code: string; isHost: boolean; playerCount: number; round: JumpingRound | null; hostUserId: string }
export interface JumpingPlayer { connectionId: string; roundId: string; username: string; height: number; phase: 'playing' | 'over'; score: number }

export function useJumpingRoom(game: RefObject<JumpingState>, commit: (next: JumpingState) => void) {
  const { session } = useAuth()
  const userId = session?.user.id
  const [room, setRoom] = useState<Membership | null>(null)
  const [players, setPlayers] = useState<ReadonlyMap<string, JumpingPlayer>>(new Map())
  const [status, setStatus] = useState<'connecting' | 'connected' | 'disconnected'>('connecting')
  const [code, setCode] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [countdown, setCountdown] = useState(0)
  const connectionRef = useRef<HubConnection | null>(null)
  const roomRef = useRef<Membership | null>(null)
  const roundRef = useRef<JumpingRound | null>(null)
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const busyRef = useRef(false)

  const clearRound = useCallback(() => {
    if (timerRef.current) clearInterval(timerRef.current)
    timerRef.current = null
    roundRef.current = null
    setCountdown(0)
    setPlayers(new Map())
    commit(createJumping())
  }, [commit])

  const finishRound = useCallback(() => {
    if (!roundRef.current || game.current.phase === 'over') return
    if (timerRef.current) clearInterval(timerRef.current)
    timerRef.current = null
    setCountdown(0)
    setNotice('Bạn đã rời màn chơi. Chờ vòng tiếp theo.')
    commit({ ...game.current, phase: 'over' })
  }, [commit, game])

  const schedule = useCallback((round: JumpingRound) => {
    clearRound()
    roundRef.current = round
    if (roomRef.current) {
      const membership = { ...roomRef.current, round }
      roomRef.current = membership
      setRoom(membership)
    }
    setError(null)
    setNotice(null)
    commit(createJumping(round.seed))
    const tick = () => {
      const remaining = Date.parse(round.startsAtUtc) - Date.now()
      setCountdown(Math.max(0, Math.ceil(remaining / 1000)))
      if (remaining <= 0) {
        if (timerRef.current) clearInterval(timerRef.current)
        timerRef.current = null
        if (document.hidden) finishRound()
        else commit(startJumping(createJumping(round.seed)))
      }
    }
    timerRef.current = setInterval(tick, 50)
    tick()
  }, [clearRound, commit, finishRound])

  const applyMembership = useCallback((membership: Membership) => {
    roomRef.current = membership
    setRoom(membership)
    setCode(membership.code)
    clearRound()
    if (membership.round && Date.parse(membership.round.startsAtUtc) > Date.now()) schedule(membership.round)
    else setNotice(membership.round ? 'Vòng đang diễn ra. Bạn sẽ chơi từ vòng tiếp theo.' : 'Chia sẻ mã phòng và chờ chủ phòng bắt đầu.')
  }, [clearRound, schedule])

  useEffect(() => {
    if (!userId) return
    let active = true
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/jumping`, { accessTokenFactory: () => getAuthSession()?.accessToken ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build()
    connectionRef.current = connection
    const interrupted = () => {
      if (!active) return
      setStatus('connecting')
      if (roomRef.current) { clearRound(); setNotice('Mất kết nối. Đang thử vào lại phòng…') }
    }
    connection.on('RoundStarted', (round: JumpingRound) => { if (roomRef.current) schedule(round) })
    connection.on('PlayerUpdated', (player: JumpingPlayer) => {
      if (player.roundId === roomRef.current?.round?.id) setPlayers(current => new Map(current).set(player.connectionId, player))
    })
    connection.on('PlayerLeft', ({ connectionId }: { connectionId: string }) => {
      setPlayers(current => { const next = new Map(current); next.delete(connectionId); return next })
    })
    connection.on('RoomUpdated', (update: { code: string; playerCount: number; hostUserId: string }) => {
      if (roomRef.current?.code !== update.code) return
      const next = { ...roomRef.current, ...update, isHost: update.hostUserId === userId }
      roomRef.current = next
      setRoom(next)
    })
    connection.onreconnecting(interrupted)
    connection.onreconnected(async () => {
      if (!active) return
      setStatus('connected')
      if (!roomRef.current) return
      try { applyMembership(await connection.invoke<Membership>('JoinRoom', roomRef.current.code)) }
      catch {
        roomRef.current = null
        setRoom(null)
        clearRound()
        setError('Phòng đã đóng sau khi mất kết nối. Hãy tạo hoặc vào phòng lại.')
      }
    })
    connection.onclose(() => {
      if (!active) return
      if (roomRef.current) clearRound()
      roomRef.current = null
      setRoom(null)
      setStatus('disconnected')
      setError('Kết nối online đã ngắt. Bạn vẫn có thể chơi đơn.')
    })
    void connection.start().then(() => { if (active) setStatus('connected') }).catch(() => {
      if (active) { setStatus('disconnected'); setError('Chưa kết nối được máy chủ online. Bạn vẫn có thể chơi đơn.') }
    })
    const updates = setInterval(() => {
      const round = roundRef.current
      const current = game.current
      if (!round || connection.state !== HubConnectionState.Connected || !['playing', 'over'].includes(current.phase)) return
      void connection.invoke('UpdatePlayer', { roundId: round.id, height: current.height, phase: current.phase, score: current.score }).catch(() => undefined)
    }, 80)
    return () => {
      active = false
      clearInterval(updates)
      if (timerRef.current) clearInterval(timerRef.current)
      timerRef.current = null
      roomRef.current = null
      roundRef.current = null
      if (connectionRef.current === connection) connectionRef.current = null
      void connection.stop()
    }
  }, [applyMembership, clearRound, game, schedule, userId])

  const run = async (method: 'CreateRoom' | 'JoinRoom' | 'StartRound' | 'LeaveRoom') => {
    const connection = connectionRef.current
    if (busyRef.current) return
    if (connection?.state !== HubConnectionState.Connected) { setError('Chờ kết nối với máy chủ online rồi thử lại.'); return }
    const normalized = code.trim().toUpperCase()
    if (method === 'JoinRoom' && !/^[A-Z0-9]{6}$/.test(normalized)) { setError('Mã phòng gồm 6 ký tự chữ hoặc số.'); return }
    busyRef.current = true
    setBusy(true)
    setError(null)
    try {
      if (method === 'JoinRoom') applyMembership(await connection.invoke<Membership>(method, normalized))
      else if (method === 'CreateRoom') applyMembership(await connection.invoke<Membership>(method))
      else {
        await connection.invoke(method)
        if (method === 'LeaveRoom') {
          roomRef.current = null
          setRoom(null)
          setCode('')
          setNotice(null)
          clearRound()
        }
      }
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Không thực hiện được. Hãy thử lại.') }
    finally { busyRef.current = false; setBusy(false) }
  }

  const reconnect = async () => {
    const connection = connectionRef.current
    if (connection?.state !== HubConnectionState.Disconnected) return
    setStatus('connecting')
    setError(null)
    try { await connection.start(); setStatus('connected') }
    catch { setStatus('disconnected'); setError('Chưa kết nối được. Hãy thử lại sau ít giây.') }
  }

  return { room, roomRef, roundRef, players, status, code, setCode, countdown, busy, error, notice, run, reconnect, finishRound }
}
