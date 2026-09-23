export default function GameSoundToggle({ enabled, onToggle }: { enabled: boolean; onToggle: () => void }) {
  return <button type="button" className="arcade-button arcade-sound" onClick={onToggle} aria-pressed={enabled} aria-label={enabled ? 'Tắt âm thanh' : 'Bật âm thanh'}>
    <svg viewBox="0 0 24 24" width="19" height="19" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true">
      <path d="M11 5 6 9H3v6h3l5 4z" strokeLinejoin="round" />
      {enabled ? <><path d="M15 8a6 6 0 0 1 0 8M18 5a10 10 0 0 1 0 14" strokeLinecap="round" /></> : <path d="m16 9 6 6m0-6-6 6" strokeLinecap="round" />}
    </svg>
    {enabled ? 'Âm thanh bật' : 'Âm thanh tắt'}
  </button>
}
