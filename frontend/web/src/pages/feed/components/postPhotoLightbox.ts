import type { MediaAccess } from '../../../api/posts'

export interface ImagePan {
  x: number
  y: number
}

export interface ImageSize {
  width: number
  height: number
}

export function clampMediaIndex(index: number, count: number) {
  if (!Number.isFinite(count) || count < 1) return -1
  const nextIndex = Number.isFinite(index) ? Math.trunc(index) : 0
  return Math.min(Math.max(0, nextIndex), Math.trunc(count) - 1)
}

export function getSelectedMediaIndex(
  media: readonly Pick<MediaAccess, 'mediaId'>[],
  selection: { mediaId: string; index: number } | null,
) {
  if (!selection) return -1
  const index = media.findIndex((item) => item.mediaId === selection.mediaId)
  return index >= 0 ? index : clampMediaIndex(selection.index, media.length)
}

export function getNeighborImageUrls(
  media: readonly (Pick<MediaAccess, 'url' | 'mediaType'> | undefined)[],
  index: number,
) {
  const current = media[index]
  if (!Number.isInteger(index) || !current) return []
  const urls = new Set<string>()
  for (const neighbor of [media[index - 1], media[index + 1]]) {
    if (neighbor?.mediaType === 'image' && neighbor.url.trim() && neighbor.url !== current.url) {
      urls.add(neighbor.url)
    }
  }
  return [...urls]
}

export function clampImagePan(pan: ImagePan, scale: number, renderedSize: ImageSize, viewportSize: ImageSize): ImagePan {
  if (scale <= 1 || !Number.isFinite(scale) ||
    [renderedSize.width, renderedSize.height, viewportSize.width, viewportSize.height]
      .some((dimension) => !Number.isFinite(dimension) || dimension <= 0)) {
    return { x: 0, y: 0 }
  }
  const limitX = Math.max(0, (renderedSize.width * scale - viewportSize.width) / 2)
  const limitY = Math.max(0, (renderedSize.height * scale - viewportSize.height) / 2)
  return {
    x: limitX === 0 ? 0 : Math.min(limitX, Math.max(-limitX, pan.x)),
    y: limitY === 0 ? 0 : Math.min(limitY, Math.max(-limitY, pan.y)),
  }
}

export function getPhotoSwipeDirection(dx: number, dy: number, scale: number, pointerType: string): -1 | 0 | 1 {
  if (pointerType !== 'touch' || scale !== 1 || Math.abs(dx) < 60 || Math.abs(dx) <= Math.abs(dy)) return 0
  return dx < 0 ? 1 : -1
}

export function isEditableTarget(target: unknown) {
  let element = target as { tagName?: string; isContentEditable?: boolean; parentElement?: typeof element | null } | null
  while (element) {
    if (element.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/i.test(element.tagName ?? '')) return true
    element = element.parentElement ?? null
  }
  return false
}
