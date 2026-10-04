import type { GameProgress, InteractionId, ObjectiveStage } from '../types.ts'

const objectives: Record<ObjectiveStage, { title: string; hint: string; step: number }> = {
  key: { title: 'Tìm chìa khóa nhà trạm', hint: 'Khám phá lán dụng cụ có ánh đèn vàng bên trái đường đi.', step: 1 },
  door: { title: 'Mở cửa trạm phát sóng', hint: 'Mang chìa khóa đến cửa chính, dưới biển số 07.', step: 2 },
  power: { title: 'Khôi phục nguồn điện', hint: 'Máy phát cũ nằm bên trái, bên trong nhà trạm.', step: 3 },
  room: { title: 'Khám phá căn phòng phía sau', hint: 'Đi qua lối cửa hẹp, theo ánh đèn xanh của máy thu.', step: 4 },
  signal: { title: 'Điều tra tín hiệu lạ', hint: 'Tiến đến máy thu trong phòng cuối và nhấn E.', step: 5 },
  complete: { title: 'Hoàn tất: tín hiệu đã được tìm thấy', hint: 'Không ai phát đi thông điệp này. Nó đang gọi tên bạn.', step: 5 },
}

export function createProgress(): GameProgress {
  return { stage: 'key', hasKey: false, doorOpen: false, powerOn: false, eventTriggered: false, noteRead: false, lampOn: true, message: '' }
}

export function getObjective(progress: GameProgress) { return objectives[progress.stage] }

export function isInteractionEnabled(progress: GameProgress, id: InteractionId): boolean {
  if (progress.stage === 'complete') return false
  switch (id) {
    case 'key': return !progress.hasKey
    case 'door': return !progress.doorOpen && !progress.eventTriggered
    case 'generator': return progress.doorOpen && !progress.powerOn
    case 'receiver': return progress.eventTriggered
    default: return true
  }
}

export function interact(progress: GameProgress, id: InteractionId): GameProgress {
  if (!isInteractionEnabled(progress, id)) return progress
  switch (id) {
    case 'key': return { ...progress, hasKey: true, stage: 'door', message: 'Đã nhặt chìa khóa trạm 07. Cửa chính ở cuối đường.' }
    case 'door': return progress.hasKey
      ? { ...progress, doorOpen: true, stage: 'power', message: 'Ổ khóa bật mở. Trong nhà trạm vẫn còn một máy phát.' }
      : { ...progress, message: 'Cửa đã khóa. Người gác trạm thường để chìa khóa trong lán dụng cụ.' }
    case 'generator': return { ...progress, powerOn: true, stage: 'room', message: 'Nguồn điện đã trở lại. Có ánh sáng từ căn phòng phía sau…' }
    case 'receiver': return { ...progress, stage: 'complete', message: '“Nếu nghe thấy tín hiệu này… đừng quay lại.”' }
    case 'note': return { ...progress, noteRead: true, message: 'GHI CHÚ — “Chìa khóa trên bàn. Máy phát ở bên trái trong trạm. Đừng trả lời nếu máy thu tự bật.”' }
    case 'lamp': return { ...progress, lampOn: !progress.lampOn, message: progress.lampOn ? 'Đèn lán đã tắt.' : 'Đèn lán đã bật.' }
  }
}
