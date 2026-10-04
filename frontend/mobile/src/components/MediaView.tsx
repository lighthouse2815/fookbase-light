import { useCallback, useEffect, useState } from 'react';
import { AppState, Image, Pressable, StyleSheet, Text, View } from 'react-native';
import { useEvent } from 'expo';
import { useFocusEffect } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import { useVideoPlayer, VideoView, type VideoContentFit } from 'expo-video';
import { useAuth } from '../auth/AuthProvider';
import { apiRequest } from '../api/client';
import type { MediaReadUrl } from '../api/media';
import { ErrorNotice, Loading } from './ui';

function Video({ url, height, nativeControls, borderRadius, contentFit, autoPlay, loop, retry }: { url: string; height: number; nativeControls: boolean; borderRadius: number; contentFit: VideoContentFit; autoPlay: boolean; loop: boolean; retry: () => void }) {
  const player = useVideoPlayer(url, value => { value.loop = loop; });
  const { isPlaying } = useEvent(player, 'playingChange', { isPlaying: player.playing });
  const { status } = useEvent(player, 'statusChange', { status: player.status });
  useFocusEffect(useCallback(() => {
    if (autoPlay && AppState.currentState === 'active') player.play();
    return () => player.pause();
  }, [player, autoPlay]));
  useEffect(() => {
    const listener = AppState.addEventListener('change', state => { if (state !== 'active') player.pause(); });
    return () => listener.remove();
  }, [player]);
  if (status === 'error') return <ErrorNotice error={new Error('Không phát được video. Hãy thử tải lại.')} retry={retry} />;
  return <View style={{ height, width: '100%', borderRadius, overflow: 'hidden' }}>
    <VideoView player={player} style={StyleSheet.absoluteFill} nativeControls={nativeControls} contentFit={contentFit} />
    {status === 'loading' && <View pointerEvents="none" style={videoStyles.loading}><Loading /></View>}
    {!nativeControls && <Pressable accessibilityRole="button" accessibilityLabel={isPlaying ? 'Tạm dừng video' : 'Phát video'} accessibilityState={{ disabled: status === 'loading' }} disabled={status === 'loading'} onPress={() => isPlaying ? player.pause() : player.play()} style={({ pressed }) => [videoStyles.control, { opacity: pressed ? 0.7 : 1 }]}><Text style={videoStyles.controlText}>{isPlaying ? 'Tạm dừng' : 'Phát video'}</Text></Pressable>}
  </View>;
}
export function MediaView({ path, height, nativeControls = true, borderRadius = 12, contentFit = 'contain', autoPlay = false, loop = false }: { path: string; height?: number; nativeControls?: boolean; borderRadius?: number; contentFit?: VideoContentFit; autoPlay?: boolean; loop?: boolean }) {
  const { session } = useAuth();
  const [imageFailed, setImageFailed] = useState(false);
  const [videoAttempt, setVideoAttempt] = useState(0);
  useEffect(() => setImageFailed(false), [path]);
  const query = useQuery({ queryKey: ['media', session?.user.id, path], queryFn: () => apiRequest<MediaReadUrl>(path), staleTime: 0 });
  if (query.error) return <ErrorNotice error={query.error} retry={() => void query.refetch()} />;
  if (!query.data) return <Loading />;
  if (imageFailed) return <ErrorNotice error={new Error('Không tải được ảnh.')} retry={() => { setImageFailed(false); void query.refetch(); }} />;
  const mediaHeight = height ?? (query.data.mediaType.toLowerCase() === 'video' ? 300 : 280);
  return query.data.mediaType.toLowerCase() === 'video' ? <Video key={`${path}-${query.data.url}-${videoAttempt}`} url={query.data.url} height={mediaHeight} nativeControls={nativeControls} borderRadius={borderRadius} contentFit={contentFit} autoPlay={autoPlay} loop={loop} retry={() => { setVideoAttempt(value => value + 1); void query.refetch(); }} /> : <Image accessibilityLabel="Ảnh đính kèm" source={{ uri: query.data.url }} style={{ width: '100%', height: mediaHeight, borderRadius }} resizeMode="contain" onError={() => setImageFailed(true)} />;
}

const videoStyles = StyleSheet.create({
  loading: { position: 'absolute', top: 0, bottom: 0, left: 0, right: 0, justifyContent: 'center', backgroundColor: '#00000080' },
  control: { position: 'absolute', top: 16, left: 16, minHeight: 44, justifyContent: 'center', paddingHorizontal: 16, borderRadius: 22, backgroundColor: '#00000099' },
  controlText: { color: '#fff', fontSize: 14, fontWeight: '700' },
});
