import { memoriesApi } from './memories'
import { apiRequest } from './client'

jest.mock('./client', () => ({ apiRequest: jest.fn() }))

test('gets only the signed-in user’s memories for today', async () => {
  jest.mocked(apiRequest).mockResolvedValueOnce({ date: '09-23', years: [] })

  await expect(memoriesApi.getToday()).resolves.toEqual({ date: '09-23', years: [] })
  expect(apiRequest).toHaveBeenCalledWith('/api/memories/today')
})
