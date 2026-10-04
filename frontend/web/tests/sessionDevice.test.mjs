import assert from 'node:assert/strict'
import test from 'node:test'
import { getSessionDeviceType } from '../src/pages/settings/sessionDevice.ts'

test('desktop browser sessions use the computer icon', () => {
  for (const agent of [
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/140.0.0.0 Safari/537.36',
    'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 Version/18.0 Safari/605.1.15',
    'Mozilla/5.0 (X11; Linux x86_64) Gecko/20100101 Firefox/140.0',
    'Mozilla/5.0 (X11; CrOS x86_64 14541.0.0) AppleWebKit/537.36 Chrome/140.0.0.0 Safari/537.36',
  ]) assert.equal(getSessionDeviceType(agent), 'computerDevice')
})

test('phone agents take precedence over their desktop OS tokens', () => {
  for (const agent of [
    'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148 Safari/604.1',
    'Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 Chrome/140.0.0.0 Mobile Safari/537.36',
    'Mozilla/5.0 (Windows Phone 10.0; Android 6.0.1) AppleWebKit/537.36 Mobile Safari/537.36',
  ]) assert.equal(getSessionDeviceType(agent), 'phoneDevice')
})

test('tablets use their own icon even when the agent includes Mobile', () => {
  for (const agent of [
    'Mozilla/5.0 (iPad; CPU OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148 Safari/604.1',
    'Mozilla/5.0 (Linux; Android 15; SM-X710) AppleWebKit/537.36 Chrome/140.0.0.0 Safari/537.36',
  ]) assert.equal(getSessionDeviceType(agent), 'tabletDevice')
})

test('missing and unrecognized agents keep the unknown device icon', () => {
  for (const agent of [null, '', '   ', 'okhttp/4.12.0', 'custom-client']) {
    assert.equal(getSessionDeviceType(agent), 'unknownDevice')
  }
})
