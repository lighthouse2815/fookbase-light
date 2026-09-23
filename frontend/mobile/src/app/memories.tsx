import { useQuery } from '@tanstack/react-query'
import { Text, View } from 'react-native'
import { memoriesApi } from '../api/memories'
import { useAuth } from '../auth/AuthProvider'
import { Card, ErrorNotice, Icon, Label, Loading, Screen, useTheme } from '../components/ui'
import { PostCard } from '../components/PostCard'

export default function Memories() {
  const { session } = useAuth()
  const theme = useTheme()
  const memories = useQuery({ queryKey: ['memories-today', session?.user.id], queryFn: memoriesApi.getToday })

  if (memories.isPending) return <Screen><Loading /></Screen>
  if (memories.error) return <Screen><ErrorNotice error={memories.error} retry={() => void memories.refetch()} /></Screen>

  const years = memories.data?.years ?? []
  return <Screen>
    <Card tone="raised" style={[memoryStyles.hero, { backgroundColor: theme.primary }]}> 
      <View style={memoryStyles.heroGlow} />
      <View style={memoryStyles.heroIcon}><Icon name="sparkle" color="#fff" size={27} /></View>
      <View style={{ flex: 1, gap: 3 }}>
        <Text style={memoryStyles.eyebrow}>NGÀY NÀY NĂM XƯA</Text>
        <Label title style={memoryStyles.heroTitle}>Kỷ niệm</Label>
        <Text style={memoryStyles.heroCopy}>Nhìn lại những điều bạn từng chia sẻ vào hôm nay.</Text>
      </View>
    </Card>
    {years.length === 0 ? <Card tone="soft" style={memoryStyles.empty}><Icon name="sparkle" color={theme.accent} size={25} /><Label style={{ fontWeight: '800' }}>Chưa có kỷ niệm cho hôm nay</Label><Text style={{ color: theme.muted, textAlign: 'center', fontSize: 13, lineHeight: 20 }}>Khi một bài viết cũ đến đúng ngày này, nó sẽ xuất hiện ở đây.</Text></Card> : years.map(year => <View key={year.year} style={memoryStyles.year}><View style={memoryStyles.yearHeading}><View><Label style={{ fontWeight: '900', fontSize: 19 }}>Năm {year.year}</Label><Text style={{ color: theme.muted, fontSize: 12 }}>{year.yearsAgo} năm trước</Text></View><View style={[memoryStyles.yearBadge, { backgroundColor: theme.primarySoft }]}><Text style={{ color: theme.primary, fontWeight: '900', fontSize: 12 }}>{year.items.length}</Text></View></View>{year.items.map(post => <PostCard key={post.id} post={post} />)}</View>)}
  </Screen>
}

const memoryStyles = {
  hero: { minHeight: 136, overflow: 'hidden' as const, flexDirection: 'row' as const, alignItems: 'center' as const, gap: 14, borderColor: 'transparent' },
  heroGlow: { position: 'absolute' as const, width: 180, height: 180, borderRadius: 100, right: -62, top: -72, backgroundColor: '#ffffff20' },
  heroIcon: { width: 52, height: 52, borderRadius: 18, alignItems: 'center' as const, justifyContent: 'center' as const, backgroundColor: '#ffffff24' },
  eyebrow: { color: '#ffffffb8', fontSize: 10, fontWeight: '900' as const, letterSpacing: 1.3 },
  heroTitle: { color: '#fff', fontSize: 26, lineHeight: 31 },
  heroCopy: { color: '#ffffffd4', fontSize: 13, lineHeight: 19 },
  empty: { alignItems: 'center' as const, gap: 8, paddingVertical: 35, paddingHorizontal: 20 },
  year: { gap: 10 },
  yearHeading: { flexDirection: 'row' as const, alignItems: 'center' as const, justifyContent: 'space-between' as const, paddingTop: 5 },
  yearBadge: { minWidth: 28, height: 28, borderRadius: 14, alignItems: 'center' as const, justifyContent: 'center' as const, paddingHorizontal: 8 },
}
