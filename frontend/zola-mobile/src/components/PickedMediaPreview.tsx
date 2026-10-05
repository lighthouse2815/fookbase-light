import { useCallback, useEffect, useState } from 'react';
import { AppState, Image } from 'react-native';
import { useFocusEffect } from 'expo-router';
import { useVideoPlayer, VideoView } from 'expo-video';
import type { PickedMedia } from '../api/media';
import { Label } from './ui';
function PickedVideo({ uri }: { uri: string }) {
  const player = useVideoPlayer(uri);
  useFocusEffect(useCallback(() => () => player.pause(), [player]));
  useEffect(() => {
    const listener = AppState.addEventListener('change', state => { if (state !== 'active') player.pause(); });
    return () => listener.remove();
  }, [player]);
  return <VideoView accessibilityLabel="Video đã chọn" player={player} style={{ width: '100%', height: 200 }} nativeControls contentFit="contain" />;
}
export function PickedMediaPreview({ file }: { file: PickedMedia }) {
  const [failed, setFailed] = useState(false);
  return <>{file.mimeType.startsWith('video/') ? <PickedVideo uri={file.uri} /> : failed ? <Label>Không đọc được tệp đã chọn. Hãy chọn lại tệp.</Label> : <Image accessibilityLabel={`Ảnh đã chọn: ${file.name}`} source={{ uri: file.uri }} style={{ width: '100%', height: 180 }} resizeMode="contain" onError={() => setFailed(true)} />}
    <Label muted>{file.name} · {Math.ceil(file.sizeBytes / 1024)} KB</Label></>;
}
