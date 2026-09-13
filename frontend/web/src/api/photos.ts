import { apiRequest } from './client'
import type { CursorPage } from './groups'

export interface PhotoAlbum { id: string; ownerUserId: string; name: string; description: string | null; albumType: 'custom'|'profilepictures'|'coverphotos'|'timelinephotos'; privacy: 'public'|'friends'|'onlyme'; photoCount: number; previewUrl: string | null; createdAtUtc: string; updatedAtUtc: string; canManage: boolean }
export interface AlbumPhoto { mediaId: string; caption: string | null; sortOrder: number; addedAtUtc: string; accessUrl: string }
export interface PhotoDetail extends AlbumPhoto { albumId: string; ownerUserId: string; url: string }
const body=(value:unknown)=>({body:JSON.stringify(value)})
const query=(cursor?:string)=>{const value=new URLSearchParams({limit:'20'});if(cursor)value.set('cursor',cursor);return value.toString()}
export const photosApi={
  create:(name:string,description:string,privacy:PhotoAlbum['privacy'])=>apiRequest<PhotoAlbum>('/api/albums',{method:'POST',...body({name,description,privacy})}),
  userAlbums:(userId:string,cursor?:string)=>apiRequest<CursorPage<PhotoAlbum>>(`/api/users/${userId}/albums?${query(cursor)}`),
  get:(albumId:string)=>apiRequest<PhotoAlbum>(`/api/albums/${albumId}`),
  media:(albumId:string,cursor?:string)=>apiRequest<CursorPage<AlbumPhoto>>(`/api/albums/${albumId}/media?${query(cursor)}`),
  addMedia:(albumId:string,mediaId:string)=>apiRequest<AlbumPhoto>(`/api/albums/${albumId}/media`,{method:'POST',...body({mediaId})}),
  photo:(albumId:string,mediaId:string)=>apiRequest<PhotoDetail>(`/api/albums/${albumId}/media/${mediaId}`),
}
