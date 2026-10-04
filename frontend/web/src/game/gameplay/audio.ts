/** Browser-native sound: no asset requests, playback begins with a user gesture. */
export class GameAudio {
  private context: AudioContext | null = null
  private output: GainNode | null = null
  private sources: AudioScheduledSourceNode[] = []
  private muted = false

  async start() {
    if (!this.context) {
      this.context = new AudioContext()
      this.output = this.context.createGain()
      this.output.gain.value = this.muted ? 0 : 0.45
      this.output.connect(this.context.destination)

      const hum = this.context.createOscillator()
      const humGain = this.context.createGain()
      hum.frequency.value = 47
      humGain.gain.value = 0.025
      hum.connect(humGain).connect(this.output)
      hum.start()

      const buffer = this.context.createBuffer(1, this.context.sampleRate * 2, this.context.sampleRate)
      const samples = buffer.getChannelData(0)
      for (let i = 0; i < samples.length; i++) samples[i] = Math.random() * 2 - 1
      const wind = this.context.createBufferSource()
      wind.buffer = buffer
      wind.loop = true
      const filter = this.context.createBiquadFilter()
      filter.type = 'lowpass'
      filter.frequency.value = 260
      const windGain = this.context.createGain()
      windGain.gain.value = 0.09
      wind.connect(filter).connect(windGain).connect(this.output)
      wind.start()
      this.sources = [hum, wind]
    }
    await this.context.resume()
  }

  pause() { void this.context?.suspend().catch(() => undefined) }

  setMuted(muted: boolean) {
    this.muted = muted
    if (this.context && this.output) this.output.gain.setTargetAtTime(muted ? 0 : 0.45, this.context.currentTime, 0.06)
  }

  cue(kind: 'item' | 'door' | 'power' | 'event' | 'signal') {
    const context = this.context
    if (!context || !this.output || context.state !== 'running') return
    const tone = context.createOscillator()
    const gain = context.createGain()
    const now = context.currentTime
    const duration = kind === 'event' ? 1.5 : kind === 'power' ? 0.8 : 0.3
    const pitch = kind === 'item' ? 660 : kind === 'signal' ? 320 : kind === 'power' ? 110 : 55
    tone.type = kind === 'event' ? 'sawtooth' : 'sine'
    tone.frequency.setValueAtTime(pitch, now)
    tone.frequency.exponentialRampToValueAtTime(kind === 'item' ? 990 : pitch * 0.45, now + duration)
    gain.gain.setValueAtTime(0.0001, now)
    gain.gain.exponentialRampToValueAtTime(kind === 'event' ? 0.055 : 0.08, now + 0.025)
    gain.gain.exponentialRampToValueAtTime(0.0001, now + duration)
    tone.connect(gain).connect(this.output)
    tone.onended = () => { tone.disconnect(); gain.disconnect() }
    tone.start(now)
    tone.stop(now + duration)
  }

  dispose() {
    for (const source of this.sources) { source.stop(); source.disconnect() }
    this.sources = []
    this.output?.disconnect()
    void this.context?.close().catch(() => undefined)
    this.context = null
    this.output = null
  }
}
