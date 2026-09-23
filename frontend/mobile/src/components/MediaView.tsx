import { useCallback, useEffect, useState } from 'react';
import { AppState, Image } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { useVideoPlayer, VideoView, type VideoContentFit } from 'expo-video';
import { useFocusEffect } from 'expo-router';
import { useAuth } from '../auth/AuthProvider';
import { apiRequest } from '../api/client';
import type { MediaReadUrl } from '../api/media';
import { ErrorNotice, Loading } from './ui';

function Video({ url, height, nativeControls, borderRadius, contentFit }: { url: string; height: number; nativeControls: boolean; borderRadius: number; contentFit: VideoContentFit }) {
  const player = useVideoPlayer(url);
  useEffect(() => {
    const listener = AppState.addEventListener('change', state => { if (state !== 'active') player.pause(); });
    return () => listener.remove();
  }, [player]);
  useFocusEffect(useCallback(() => () => { player.pause(); }, [player]));
  return <VideoView player={player} style={{ height, width: '100%', borderRadius }} nativeControls={nativeControls} contentFit={contentFit} />;
}
export function MediaView({ path, height, nativeControls = true, borderRadius = 12, contentFit = 'contain' }: { path: string; height?: number; nativeControls?: boolean; borderRadius?: number; contentFit?: VideoContentFit }) {
  const { session } = useAuth();
  const [imageFailed, setImageFailed] = useState(false);
  useEffect(() => setImageFailed(false), [path]);
  const query = useQuery({ queryKey: ['media', session?.user.id, path], queryFn: () => apiRequest<MediaReadUrl>(path), staleTime: 0 });
  if (query.error) return <ErrorNotice error={query.error} retry={() => void query.refetch()} />;
  if (!query.data) return <Loading />;
  if (imageFailed) return <ErrorNotice error={new Error('Không tải được ảnh.')} retry={() => { setImageFailed(false); void query.refetch(); }} />;
  const mediaHeight = height ?? (query.data.mediaType.toLowerCase() === 'video' ? 300 : 280);
  return query.data.mediaType.toLowerCase() === 'video' ? <Video url={query.data.url} height={mediaHeight} nativeControls={nativeControls} borderRadius={borderRadius} contentFit={contentFit} /> : <Image accessibilityLabel="Ảnh đính kèm" source={{ uri: query.data.url }} style={{ width: '100%', height: mediaHeight, borderRadius }} resizeMode="contain" onError={() => setImageFailed(true)} />;
}
