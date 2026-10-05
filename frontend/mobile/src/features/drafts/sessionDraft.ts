import { useSyncExternalStore } from 'react';
import { getSession, getSessionGeneration, subscribeSession } from '../../auth/session';

// Session memory only: media references never go into persistent storage.
const drafts = new Map<string, unknown>();
const listeners = new Set<() => void>();
let owner = getSession()?.user.id;
let generation = getSessionGeneration();
let epoch = 0;
function emit() { listeners.forEach(listener => listener()); }
subscribeSession(() => {
  const nextOwner = getSession()?.user.id;
  const nextGeneration = getSessionGeneration();
  if (owner !== nextOwner || generation !== nextGeneration) {
    drafts.clear(); epoch++; owner = nextOwner; generation = nextGeneration; emit();
  }
});
function subscribe(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener); }; }
export function useSessionDraft<T>(accountId: string | undefined, context: string, empty: T) {
  const expectedEpoch = epoch;
  const key = JSON.stringify([accountId, context]);
  const current = () => accountId !== undefined && getSession()?.user.id === accountId && epoch === expectedEpoch;
  const value = useSyncExternalStore(subscribe, () => current() ? (drafts.get(key) as T | undefined) ?? empty : empty);
  const update = (next: T | ((previous: T) => T)) => {
    if (!current()) return;
    drafts.set(key, typeof next === 'function' ? (next as (previous: T) => T)((drafts.get(key) as T | undefined) ?? empty) : next);
    emit();
  };
  const discard = () => { if (current()) { drafts.delete(key); emit(); } };
  return [value, update, discard, current] as const;
}
