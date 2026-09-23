import { File } from 'expo-file-system';
import { fetch as nativeFetch } from 'expo/fetch';
import * as ImagePicker from 'expo-image-picker';
import { apiRequest } from './client';
export interface MediaReadUrl { mediaId: string; url: string; expiresAtUtc: string; mediaType: string; contentType: string }
export interface MediaAsset {
  id: string
  ownerUserId: string
  mediaType: string
  status: string
  fileName: string
  contentType: string
  declaredSizeBytes: number
  actualSizeBytes: number | null
  createdAtUtc: string
  uploadExpiresAtUtc: string | null
  uploadedAtUtc: string | null
  deletedAtUtc: string | null
  durationMs: number | null
  width: number | null
  height: number | null
  hasProcessedVideo: boolean
  processedAtUtc: string | null
}
export interface PickedMedia { uri: string; name: string; mimeType: string; sizeBytes: number }
export async function pickMedia(): Promise<PickedMedia | null> {
  const result = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images', 'videos'], quality: 1 });
  if (result.canceled) return null;
  const asset = result.assets[0]; const file = new File(asset.uri);
  const mimeType = asset.mimeType || file.type;
  const sizeBytes = asset.fileSize ?? file.size;
  if (!sizeBytes || !mimeType || (!mimeType.startsWith('image/') && !mimeType.startsWith('video/'))) throw new Error('Không đọc được định dạng hoặc kích thước ảnh/video.');
  return { uri: asset.uri, name: asset.fileName || file.name, mimeType, sizeBytes };
}
export async function uploadMedia(file: PickedMedia): Promise<string> {
  const intent = await apiRequest<{ mediaId: string; uploadUrl: string; uploadMethod: 'POST'; uploadParameters: Record<string, string> }>('/api/media/uploads', {
    method: 'POST', body: JSON.stringify({ fileName: file.name, contentType: file.mimeType, sizeBytes: file.sizeBytes }),
  });
  if (new URL(intent.uploadUrl).protocol !== 'https:') throw new Error('Máy chủ trả địa chỉ upload không an toàn.');
  const body = new FormData();
  Object.entries(intent.uploadParameters).forEach(([key, value]) => body.append(key, value));
  body.append('file', new File(file.uri), file.name);
  const response = await nativeFetch(intent.uploadUrl, { method: intent.uploadMethod, body });
  if (!response.ok) throw new Error('Tải media thất bại. Nội dung soạn vẫn được giữ.');
  await apiRequest(`/api/media/${intent.mediaId}/complete`, { method: 'POST' });
  return intent.mediaId;
}

export const mediaApi = {
  getMetadata: (mediaId: string) => apiRequest<MediaAsset>(`/api/media/${mediaId}`),
  getAccess: (mediaId: string) => apiRequest<MediaReadUrl>(`/api/media/${mediaId}/access`),
  getPosterAccess: (mediaId: string) => apiRequest<MediaReadUrl>(`/api/media/${mediaId}/poster/access`),
  delete: (mediaId: string) => apiRequest<void>(`/api/media/${mediaId}`, { method: 'DELETE' }),
}
