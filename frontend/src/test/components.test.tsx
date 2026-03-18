import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import RoomButton from '../components/RoomButton'
import RoomSummaryBar from '../components/RoomSummaryBar'
import type { Room } from '../types'

// ── RoomButton tests (BR-11, BR-16, BR-17, BR-25) ─────────────────────────────

describe('RoomButton', () => {
  const baseRoom: Room = {
    id: 1, roomShortName: '101', roomLongName: '',
    roomStatus: 'Open', roomType: 'SINGLE BED ROOM',
    roomLocation: 'Level 1', roomPrice: 100, breakfast: true,
    breakfastPrice: 10, maintenance: false, active: true, bookingId: 0,
  }

  it('renders room number and type', () => {
    render(<RoomButton room={baseRoom} index={1} blink={false} onClick={() => {}} />)
    expect(screen.getByText('101')).toBeInTheDocument()
    expect(screen.getByText('SINGLE BED ROOM')).toBeInTheDocument()
  })

  it('shows status text', () => {
    render(<RoomButton room={baseRoom} index={1} blink={false} onClick={() => {}} />)
    expect(screen.getByText('Open')).toBeInTheDocument()
  })

  it('calls onClick when clicked', async () => {
    const handler = vi.fn()
    render(<RoomButton room={baseRoom} index={1} blink={false} onClick={handler} />)
    await userEvent.click(screen.getByRole('button'))
    expect(handler).toHaveBeenCalledOnce()
  })

  it('renders unconfigured state when room is null (BR-17)', () => {
    render(<RoomButton room={null} index={5} blink={false} onClick={() => {}} />)
    expect(screen.getByText('005')).toBeInTheDocument()
    expect(screen.getByText('Not set')).toBeInTheDocument()
  })

  it('renders Maintenance room (BR-16)', () => {
    const maintRoom = { ...baseRoom, roomStatus: 'Maintenance' as const }
    render(<RoomButton room={maintRoom} index={1} blink={false} onClick={() => {}} />)
    expect(screen.getByText('Maintenance')).toBeInTheDocument()
  })
})

// ── RoomSummaryBar tests ───────────────────────────────────────────────────────

describe('RoomSummaryBar', () => {
  it('displays all status counts', () => {
    const summary = { open: 5, booked: 3, occupied: 2, housekeeping: 1, maintenance: 0 }
    render(<RoomSummaryBar summary={summary} />)
    expect(screen.getByText('5')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('2')).toBeInTheDocument()
    expect(screen.getByText('1')).toBeInTheDocument()
  })

  it('displays zero counts', () => {
    const summary = { open: 0, booked: 0, occupied: 0, housekeeping: 0, maintenance: 0 }
    render(<RoomSummaryBar summary={summary} />)
    const zeros = screen.getAllByText('0')
    expect(zeros.length).toBe(5)
  })
})