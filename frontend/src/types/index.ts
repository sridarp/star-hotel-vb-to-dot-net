// Domain types matching backend DTOs

export type RoomStatus = 'Open' | 'Booked' | 'Occupied' | 'Housekeeping' | 'Maintenance' | 'Void'

export interface Room {
  id: number
  roomShortName: string
  roomLongName: string
  roomStatus: RoomStatus
  roomType: string
  roomLocation: string
  roomPrice: number
  breakfast: boolean
  breakfastPrice: number
  maintenance: boolean
  active: boolean
  bookingId: number
}

export interface RoomSummary {
  open: number
  booked: number
  occupied: number
  housekeeping: number
  maintenance: number
}

export interface Booking {
  id: number
  formattedId: string
  guestName: string
  guestPassport: string
  guestOrigin?: string
  guestContact?: string
  guestEmergencyContactName?: string
  guestEmergencyContactNo?: string
  totalGuest: number
  stayDuration: number
  bookingDate: string
  guestCheckIn: string
  guestCheckOut: string
  remarks?: string
  roomId: number
  roomNo: string
  roomType: string
  roomLocation: string
  roomPrice: number
  breakfast: boolean
  breakfastPrice: number
  subTotal: number
  deposit: number
  payment: number
  refund: number
  totalDue: number
  active: boolean
  temp: boolean
  createdDate: string
  createdBy: string
  lastModifiedDate?: string
  lastModifiedBy: string
}

export interface SaveBookingRequest {
  bookingId: number
  roomId: number
  guestName: string
  guestPassport: string
  guestOrigin?: string
  guestContact?: string
  guestEmergencyContactName?: string
  guestEmergencyContactNo?: string
  totalGuest: number
  stayDuration: number
  bookingDate: string
  guestCheckIn: string
  guestCheckOut: string
  remarks?: string
  deposit: number
  payment: number
}

export interface PriceCalculation {
  subTotal: number
  totalDue: number
  checkOutDate: string
  refund: number
  defaultDeposit: number
}

export interface User {
  id: number
  userId: string
  userName: string
  userGroup: number
  idle: number
  loginAttempts: number
  changePassword: boolean
  dashboardBlink: boolean
  active: boolean
}

export interface AuthTokens {
  accessToken: string
  userId: string
  userName: string
  userGroup: number
  expiresAt: string
}

// Room status colour mapping (matches VB6 colour constants)
export const ROOM_COLORS: Record<RoomStatus, string> = {
  Open: '#76e600',
  Booked: '#ffea00',
  Occupied: '#ff1744',
  Housekeeping: '#d500f9',
  Maintenance: '#ff7929',
  Void: '#505050',
}

export const ROOM_TEXT_COLORS: Record<RoomStatus, string> = {
  Open: '#000',
  Booked: '#000',
  Occupied: '#fff',
  Housekeeping: '#fff',
  Maintenance: '#fff',
  Void: '#aaa',
}