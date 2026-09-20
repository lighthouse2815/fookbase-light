import { useCallback, useEffect } from 'react';
import { AppState, Image } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { useVideoPlayer, VideoView } from 'expo-video';
import { useFocusEffect } from 'expo-router';
import { useAuth } from '../auth/AuthProvider';
import { apiRequest } from '../api/client';
import type { MediaReadUrl } from '../api/media';
import { ErrorNotice, Loading } from './ui';

function Video({ url }: { url: string }) {
  const player = useVideoPlayer(url);
  useEffect(() => {
    const listener = AppState.addEventListener('change', state => { if (state !== 'active') player.pause(); });
    return () => listener.remove();
  }, [player]);
  useFocusEffect(useCallback(() => () => { player.pause(); }, [player]));
  return <VideoView player={player} style={{ height: 300, width: '100%', borderRadius: 12 }} nativeControls />;
}
export function MediaView({ path }: { path: string }) {
  const { session } = useAuth();
  const query = useQuery({ queryKey: ['media', session?.user.id, path], queryFn: () => apiRequest<MediaReadUrl>(path), staleTime: 0 });
  if (query.error) return <ErrorNotice error={query.error} retry={() => void query.refetch()} />;
  if (!query.data) return <Loading />;
  return query.data.mediaType.toLowerCase() === 'video' ? <Video url={query.data.url} /> : <Image accessibilityLabel="Ảnh đính kèm" source={{ uri: query.data.url }} style={{ width: '100%', height: 280, borderRadius: 12 }} resizeMode="contain" onError={() => { if (!query.isFetching) void query.refetch(); }} />;
}
