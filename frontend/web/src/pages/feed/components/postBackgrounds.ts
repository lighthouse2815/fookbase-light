export const POST_BACKGROUNDS = [
  { id: 'purple', label: 'Tím', swatch: 'bg-[#b500e8]', className: 'bg-linear-to-br from-[#8f00d4] via-[#bd00dc] to-[#ef3b8f]' },
  { id: 'pink', label: 'Hồng', swatch: 'bg-[#ed003e]', className: 'bg-linear-to-br from-[#d8005b] via-[#ed003e] to-[#ff6a3d]' },
  { id: 'midnight', label: 'Đêm', swatch: 'bg-[#15171b]', className: 'bg-linear-to-br from-[#111318] via-[#292d38] to-[#414755]' },
  { id: 'sunset', label: 'Hoàng hôn', swatch: 'bg-linear-to-br from-[#ff3d71] to-[#ffad32]', className: 'bg-linear-to-br from-[#de176b] via-[#f34655] to-[#ffb02e]' },
  { id: 'ocean', label: 'Đại dương', swatch: 'bg-linear-to-br from-[#1268e8] to-[#18c2d8]', className: 'bg-linear-to-br from-[#0759cb] via-[#078fd5] to-[#20cfca]' },
  { id: 'neon', label: 'Neon', swatch: 'bg-linear-to-br from-[#7028df] to-[#f43ec2]', className: 'bg-linear-to-br from-[#4a22bb] via-[#9c2de0] to-[#f54db7]' },
] as const

export const getPostBackgroundClass = (id?: string | null) =>
  POST_BACKGROUNDS.find((background) => background.id === id)?.className
