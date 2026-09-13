import { useEffect } from 'react'
import type { AlbumPhoto, PhotoDetail } from '../../api/photos'

interface Props { item: AlbumPhoto; detail: PhotoDetail | null; onClose: () => void; onPrevious: () => void; onNext: () => void; hasPrevious: boolean; hasNext: boolean }
export default function PhotoViewer({ item, detail, onClose, onPrevious, onNext, hasPrevious, hasNext }: Props) {
  useEffect(() => { const listener=(event:KeyboardEvent)=>{ if(event.key==='Escape')onClose(); if(event.key==='ArrowLeft')onPrevious(); if(event.key==='ArrowRight')onNext() }; window.addEventListener('keydown',listener); return()=>window.removeEventListener('keydown',listener) },[onClose,onPrevious,onNext])
  return <div role="dialog" aria-modal="true" className="fixed inset-0 z-50 grid place-items-center bg-black/85 p-4"><button onClick={onClose} className="absolute right-5 top-5 text-2xl text-white">×</button><div className="flex max-h-full max-w-5xl items-center gap-3"><button disabled={!hasPrevious} onClick={onPrevious} className="rounded bg-white/15 px-3 py-2 text-white disabled:opacity-30">←</button><div><img src={detail?.url ?? item.accessUrl} alt={item.caption ?? ''} className="max-h-[75vh] max-w-[80vw] rounded object-contain"/>{item.caption&&<p className="mt-3 text-center text-white">{item.caption}</p>}</div><button disabled={!hasNext} onClick={onNext} className="rounded bg-white/15 px-3 py-2 text-white disabled:opacity-30">→</button></div></div>
}
