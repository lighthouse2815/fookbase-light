import { useState } from 'react'
import { Pressable, StyleSheet, Text, View } from 'react-native'
import { Image } from 'expo-image'
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi, type Event, type EventInvitation } from '../../api/events'
import { useAuth } from '../../auth/AuthProvider'
import { Avatar, Button, Card, ErrorNotice, Field, Icon, Label, Loading, Screen, styles, useTheme } from '../../components/ui'

type EventMode = 'upcoming' | 'discover' | 'mine'

const modeLabels: Record<EventMode, string> = {
  upcoming: 'Sắp diễn ra',
  discover: 'Khám phá',
  mine: 'Của bạn',
}

function eventDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function EventCard({ event, pending, onRsvp }: { event: Event; pending: boolean; onRsvp: (status: 'going' | 'interested' | null) => void }) {
  const theme = useTheme()
  const activity = event.status === 'cancelled' ? 'Đã hủy' : event.viewerRsvpStatus === 'going' ? 'Bạn sẽ tham gia' : event.viewerRsvpStatus === 'interested' ? 'Bạn quan tâm' : null
  return <Card style={eventStyles.eventCard}>
    {event.coverUrl ? <Image source={{ uri: event.coverUrl }} contentFit="cover" style={eventStyles.cover} /> : <View style={[eventStyles.cover, { backgroundColor: `${theme.accent}20` }]}><Icon name="sparkle" color={theme.accent} size={32} /></View>}
    <View style={eventStyles.content}>
      <Text style={{ color: theme.primary, fontSize: 12, fontWeight: '900', letterSpacing: 0.4 }}>{eventDate(event.startsAtUtc).toUpperCase()}</Text>
      <Label style={eventStyles.name} numberOfLines={2}>{event.name}</Label>
      <View style={eventStyles.meta}><Icon name={event.locationType === 'online' ? 'globe' : 'people'} color={theme.muted} size={15} /><Text style={{ color: theme.muted, fontSize: 12 }} numberOfLines={1}>{event.locationType === 'online' ? 'Sự kiện trực tuyến' : event.locationName || event.address || 'Địa điểm sẽ được cập nhật'}</Text></View>
      <View style={eventStyles.meta}><Avatar label={event.displayHost.name} uri={event.displayHost.avatarUrl} size={20} /><Text style={{ color: theme.muted, fontSize: 12 }} numberOfLines={1}>Bởi {event.displayHost.name} · {event.goingCount.toLocaleString('vi-VN')} sẽ tham gia</Text></View>
      {activity && <View style={[eventStyles.status, { backgroundColor: `${event.status === 'cancelled' ? theme.danger : theme.success}1c` }]}><Text style={{ color: event.status === 'cancelled' ? theme.danger : theme.success, fontSize: 12, fontWeight: '800' }}>{activity}</Text></View>}
      {event.status !== 'cancelled' && <View style={styles.row}><Button compact title={event.viewerRsvpStatus === 'going' ? 'Không tham gia' : 'Tham gia'} secondary={event.viewerRsvpStatus === 'going'} disabled={pending} onPress={() => onRsvp(event.viewerRsvpStatus === 'going' ? null : 'going')} /><Button compact title={event.viewerRsvpStatus === 'interested' ? 'Bỏ quan tâm' : 'Quan tâm'} secondary={event.viewerRsvpStatus === 'interested'} disabled={pending} onPress={() => onRsvp(event.viewerRsvpStatus === 'interested' ? null : 'interested')} /></View>}
    </View>
  </Card>
}

function InvitationCard({ invitation, pending, onAccept, onDecline }: { invitation: EventInvitation; pending: boolean; onAccept: () => void; onDecline: () => void }) {
  const theme = useTheme()
  if (!invitation.event) return null
  return <Card tone="soft" style={eventStyles.invitation}><View style={[eventStyles.inviteIcon, { backgroundColor: `${theme.primary}1c` }]}><Icon name="bell" color={theme.primary} size={19} /></View><View style={{ flex: 1, gap: 2 }}><Label style={{ fontWeight: '800' }} numberOfLines={1}>{invitation.event.name}</Label><Text style={{ color: theme.muted, fontSize: 12 }}>Bạn có một lời mời sự kiện</Text></View><Pressable accessibilityRole="button" accessibilityLabel="Từ chối lời mời sự kiện" disabled={pending} onPress={onDecline}><Icon name="close" color={theme.muted} size={20} /></Pressable><Button compact title="Xem" disabled={pending} onPress={onAccept} /></Card>
}

export default function EventsScreen() {
  const { session } = useAuth()
  const cache = useQueryClient()
  const [mode, setMode] = useState<EventMode>('upcoming')
  const [input, setInput] = useState('')
  const [search, setSearch] = useState('')
  const events = useInfiniteQuery({
    queryKey: ['events', session?.user.id, mode, search], initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam, signal }) => mode === 'upcoming' ? eventsApi.upcoming(pageParam, { signal }) : mode === 'mine' ? eventsApi.mine(pageParam, { signal }) : eventsApi.discover(search, pageParam, { signal }),
    getNextPageParam: page => page.nextCursor ?? undefined,
  })
  const invitations = useInfiniteQuery({ queryKey: ['event-invitations', session?.user.id], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam, signal }) => eventsApi.invitations(pageParam, { signal }), getNextPageParam: page => page.nextCursor ?? undefined })
  const rsvp = useMutation({ mutationFn: async ({ event, status }: { event: Event; status: 'going' | 'interested' | null }) => { if (status) await eventsApi.rsvp(event.id, status); else await eventsApi.removeRsvp(event.id) }, onSuccess: () => cache.invalidateQueries({ queryKey: ['events'] }) })
  const inviteAction = useMutation({ mutationFn: (work: () => Promise<unknown>) => work(), onSuccess: () => cache.invalidateQueries({ queryKey: ['event-invitations'] }) })
  const items = events.data?.pages.flatMap(page => page.items) ?? []
  const pendingInvitations = invitations.data?.pages.flatMap(page => page.items) ?? []
  const theme = useTheme()

  return <Screen>
    <View style={eventStyles.hero}><View style={[eventStyles.heroIcon, { backgroundColor: `${theme.accent}22` }]}><Icon name="sparkle" color={theme.accent} size={27} /></View><View style={{ flex: 1 }}><Label title style={{ fontSize: 26 }}>Sự kiện</Label><Text style={{ color: theme.muted, fontSize: 13 }}>Đừng bỏ lỡ những cuộc gặp gỡ sắp tới.</Text></View></View>
    {pendingInvitations.length > 0 && <View style={{ gap: 8 }}><Label style={{ fontWeight: '800' }}>Lời mời dành cho bạn</Label>{pendingInvitations.map(invitation => <InvitationCard key={invitation.id} invitation={invitation} pending={inviteAction.isPending} onAccept={() => inviteAction.mutate(() => eventsApi.acceptInvitation(invitation.id))} onDecline={() => inviteAction.mutate(() => eventsApi.declineInvitation(invitation.id))} />)}</View>}
    <View style={[eventStyles.modeTabs, { backgroundColor: theme.surface2, borderColor: theme.border }]}>{(Object.keys(modeLabels) as EventMode[]).map(value => <Pressable key={value} accessibilityRole="button" accessibilityState={{ selected: mode === value }} onPress={() => setMode(value)} style={[eventStyles.modeTab, mode === value && { backgroundColor: theme.card }]}><Text style={{ color: mode === value ? theme.primary : theme.muted, fontWeight: '800', fontSize: 12 }}>{modeLabels[value]}</Text></Pressable>)}</View>
    {mode === 'discover' && <View style={eventStyles.searchRow}><View style={{ flex: 1 }}><Field label="Tìm sự kiện" value={input} onChangeText={setInput} placeholder="Tên, địa điểm hoặc chủ đề" returnKeyType="search" onSubmitEditing={() => setSearch(input)} /></View><Button compact title="Tìm" onPress={() => setSearch(input)} /></View>}
    {events.isPending && <Loading />}{items.map(event => <EventCard key={event.id} event={event} pending={rsvp.isPending} onRsvp={status => rsvp.mutate({ event, status })} />)}
    {!events.isPending && !events.error && items.length === 0 && <Card tone="soft" style={eventStyles.empty}><Icon name="sparkle" color={theme.accent} size={24} /><Label style={{ fontWeight: '800' }}>Chưa có sự kiện ở đây</Label><Text style={{ color: theme.muted, textAlign: 'center', fontSize: 13 }}>Hãy thử khám phá thêm các cộng đồng và chủ đề bạn thích.</Text></Card>}
    {events.error && <ErrorNotice error={events.error} retry={() => void events.refetch()} />}{rsvp.error && <ErrorNotice error={rsvp.error} />}{inviteAction.error && <ErrorNotice error={inviteAction.error} />}{events.hasNextPage && <Button secondary title={events.isFetchingNextPage ? 'Đang tải…' : 'Xem thêm sự kiện'} disabled={events.isFetchingNextPage} onPress={() => void events.fetchNextPage()} />}
  </Screen>
}

const eventStyles = StyleSheet.create({
  hero: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  heroIcon: { width: 56, height: 56, borderRadius: 19, alignItems: 'center', justifyContent: 'center' },
  modeTabs: { flexDirection: 'row', padding: 4, gap: 4, borderRadius: 15, borderWidth: 1 },
  modeTab: { flex: 1, minHeight: 38, borderRadius: 11, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 5 },
  searchRow: { flexDirection: 'row', alignItems: 'flex-end', gap: 8 },
  eventCard: { padding: 0, overflow: 'hidden', gap: 0 },
  cover: { width: '100%', height: 128, alignItems: 'center', justifyContent: 'center' },
  content: { padding: 14, gap: 8 },
  name: { fontSize: 18, lineHeight: 23, fontWeight: '900' },
  meta: { flexDirection: 'row', alignItems: 'center', gap: 7 },
  status: { alignSelf: 'flex-start', borderRadius: 9, paddingHorizontal: 9, paddingVertical: 5 },
  invitation: { padding: 11, flexDirection: 'row', alignItems: 'center', gap: 9 },
  inviteIcon: { width: 40, height: 40, borderRadius: 14, alignItems: 'center', justifyContent: 'center' },
  empty: { alignItems: 'center', gap: 8, paddingVertical: 28, paddingHorizontal: 22 },
})
