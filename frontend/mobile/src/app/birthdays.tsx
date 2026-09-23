import { router } from 'expo-router'
import { useQuery } from '@tanstack/react-query'
import { Pressable, Text, View } from 'react-native'
import { birthdaysApi, resolveProfileImageUrl, type BirthdayFriend } from '../api/users'
import { useAuth } from '../auth/AuthProvider'
import { Avatar, Card, ErrorNotice, Icon, Label, Loading, Screen, useTheme } from '../components/ui'

function BirthdayRow({ person, today }: { person: BirthdayFriend; today?: boolean }) {
  const theme = useTheme()
  const date = new Intl.DateTimeFormat('vi-VN', { day: 'numeric', month: 'long' }).format(new Date(2000, person.month - 1, person.day))
  return <Pressable accessibilityRole="button" accessibilityLabel={`Xem trang cá nhân ${person.displayName}`} onPress={() => router.push(`/profile/${person.userId}`)} style={({ pressed }) => ({ opacity: pressed ? 0.72 : 1 })}>
    <Card style={birthdayStyles.row}>
      <Avatar label={person.displayName} uri={person.avatarUrl ? resolveProfileImageUrl(person.avatarUrl) : null} size={48} />
      <View style={{ flex: 1, gap: 2 }}><Label style={{ fontWeight: '800', fontSize: 15 }}>{person.displayName}</Label><Text style={{ color: theme.muted, fontSize: 12 }}>{today ? 'Sinh nhật hôm nay' : date}</Text></View>
      <View style={[birthdayStyles.cake, { backgroundColor: `${theme.accent}24` }]}><Text style={{ fontSize: 20 }}>🎂</Text></View>
    </Card>
  </Pressable>
}

export default function Birthdays() {
  const { session } = useAuth()
  const theme = useTheme()
  const today = useQuery({ queryKey: ['birthdays-today', session?.user.id], queryFn: birthdaysApi.getToday })
  const upcoming = useQuery({ queryKey: ['birthdays-upcoming', session?.user.id], queryFn: () => birthdaysApi.getUpcoming(7) })
  const errors = [today.error, upcoming.error].filter(Boolean)
  const upcomingPeople = (upcoming.data ?? []).filter(person => !(today.data ?? []).some(todayPerson => todayPerson.userId === person.userId))

  return <Screen>
    <Card tone="raised" style={[birthdayStyles.hero, { backgroundColor: `${theme.accent}20`, borderColor: `${theme.accent}44` }]}>
      <View style={[birthdayStyles.heroIcon, { backgroundColor: `${theme.accent}2e` }]}><Text style={{ fontSize: 28 }}>🎉</Text></View>
      <View style={{ flex: 1, gap: 3 }}><Label title style={{ fontSize: 24 }}>Sinh nhật bạn bè</Label><Text style={{ color: theme.muted, fontSize: 13, lineHeight: 19 }}>Gửi một lời chúc đúng lúc cho những người bạn thân quen.</Text></View>
    </Card>
    {today.isPending || upcoming.isPending ? <Loading /> : <>
      <View style={birthdayStyles.heading}><Label style={{ fontWeight: '900', fontSize: 19 }}>Hôm nay</Label><Text style={{ color: theme.muted, fontSize: 12 }}>{today.data?.length ?? 0} sinh nhật</Text></View>
      {today.data?.length ? today.data.map(person => <BirthdayRow key={person.userId} person={person} today />) : <Card tone="soft"><Text style={{ color: theme.muted, textAlign: 'center', fontSize: 13 }}>Hôm nay chưa có sinh nhật nào trong danh sách bạn bè.</Text></Card>}
      <View style={birthdayStyles.heading}><Label style={{ fontWeight: '900', fontSize: 19 }}>7 ngày tới</Label><Icon name="arrow" color={theme.muted} size={17} /></View>
      {upcomingPeople.length ? upcomingPeople.map(person => <BirthdayRow key={person.userId} person={person} />) : <Card tone="soft"><Text style={{ color: theme.muted, textAlign: 'center', fontSize: 13 }}>Chưa có sinh nhật nào sắp tới.</Text></Card>}
    </>}
    {errors[0] && <ErrorNotice error={errors[0]} retry={() => { void today.refetch(); void upcoming.refetch() }} />}
  </Screen>
}

const birthdayStyles = {
  hero: { flexDirection: 'row' as const, alignItems: 'center' as const, gap: 13 },
  heroIcon: { width: 56, height: 56, borderRadius: 19, alignItems: 'center' as const, justifyContent: 'center' as const },
  heading: { flexDirection: 'row' as const, alignItems: 'center' as const, justifyContent: 'space-between' as const, marginTop: 5 },
  row: { flexDirection: 'row' as const, alignItems: 'center' as const, gap: 12, padding: 13 },
  cake: { width: 40, height: 40, borderRadius: 14, alignItems: 'center' as const, justifyContent: 'center' as const },
}
