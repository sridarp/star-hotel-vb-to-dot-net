import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { bookingApi, roomApi } from '../services/api'
import { useAuthStore } from '../store/authStore'
import type { SaveBookingRequest } from '../types'
import { format } from 'date-fns'

const DEFAULT_DEPOSIT = 20

export default function BookingPage() {
  const { roomId, bookingId } = useParams<{ roomId: string; bookingId?: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const userId = useAuthStore(s => s.userId) ?? ''

  const roomIdNum = parseInt(roomId ?? '0')
  const bookingIdNum = bookingId ? parseInt(bookingId) : undefined

  const { data: room } = useQuery({
    queryKey: ['room', roomIdNum],
    queryFn: () => roomApi.get(roomIdNum),
    enabled: roomIdNum > 0,
  })

  const { data: booking } = useQuery({
    queryKey: ['booking', bookingIdNum],
    queryFn: () => bookingApi.get(bookingIdNum!),
    enabled: !!bookingIdNum,
  })

  // Form state
  const [form, setForm] = useState({
    guestName: '',
    guestPassport: '',
    guestOrigin: '',
    guestContact: '',
    guestEmergencyContactName: '',
    guestEmergencyContactNo: '',
    totalGuest: 1,
    stayDuration: 1,
    bookingDate: format(new Date(), "yyyy-MM-dd'T'HH:mm"),
    guestCheckIn: format(new Date(), "yyyy-MM-dd'T'HH:mm"),
    guestCheckOut: '',
    remarks: '',
    deposit: DEFAULT_DEPOSIT,
    payment: 0,
    refund: 0,
  })
  const [tempBookingId, setTempBookingId] = useState<number | null>(null)
  const [subTotal, setSubTotal] = useState(0)
  const [totalDue, setTotalDue] = useState(DEFAULT_DEPOSIT)

  // Create temp booking on mount if no existing booking
  useEffect(() => {
    if (!bookingIdNum) {
      bookingApi.createTemp().then(r => setTempBookingId(r.bookingId))
    }
  }, [bookingIdNum])

  // Populate from existing booking
  useEffect(() => {
    if (booking) {
      setForm({
        guestName: booking.guestName,
        guestPassport: booking.guestPassport,
        guestOrigin: booking.guestOrigin ?? '',
        guestContact: booking.guestContact ?? '',
        guestEmergencyContactName: booking.guestEmergencyContactName ?? '',
        guestEmergencyContactNo: booking.guestEmergencyContactNo ?? '',
        totalGuest: booking.totalGuest,
        stayDuration: booking.stayDuration,
        bookingDate: booking.bookingDate.substring(0, 16),
        guestCheckIn: booking.guestCheckIn.substring(0, 16),
        guestCheckOut: booking.guestCheckOut.substring(0, 16),
        remarks: booking.remarks ?? '',
        deposit: booking.deposit,
        payment: booking.payment,
        refund: booking.refund,
      })
      setSubTotal(booking.subTotal)
      setTotalDue(booking.totalDue)
    }
  }, [booking])

  // BR-18: Recalculate on duration or room price change
  useEffect(() => {
    if (room) {
      const st = form.stayDuration * room.roomPrice
      setSubTotal(st)
      setTotalDue(st + form.deposit)
    }
  }, [form.stayDuration, form.deposit, room])

  const effectivBookingId = bookingIdNum ?? tempBookingId ?? 0

  const saveMutation = useMutation({
    mutationFn: (req: SaveBookingRequest) => bookingApi.save(effectivBookingId, req),
    onSuccess: () => {
      toast.success('Booking saved!')
      queryClient.invalidateQueries({ queryKey: ['rooms'] })
    },
    onError: (e: unknown) => toast.error((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Save failed'),
  })

  const checkInMutation = useMutation({
    mutationFn: () => bookingApi.checkIn(effectivBookingId, new Date(form.guestCheckIn).toISOString()),
    onSuccess: () => { toast.success('Checked In!'); queryClient.invalidateQueries({ queryKey: ['rooms'] }) },
    onError: (e: unknown) => toast.error((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Check-in failed'),
  })

  const checkOutMutation = useMutation({
    mutationFn: () => bookingApi.checkOut(effectivBookingId, new Date(form.guestCheckOut).toISOString(), form.refund),
    onSuccess: () => { toast.success('Checked Out! Room → Housekeeping'); queryClient.invalidateQueries({ queryKey: ['rooms'] }) },
    onError: (e: unknown) => toast.error((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? 'Check-out failed'),
  })

  const handleSave = () => {
    if (!form.guestName.trim()) { toast.error('Guest Name is required'); return }
    if (!form.guestPassport.trim()) { toast.error('Passport/IC No is required'); return }

    const req: SaveBookingRequest = {
      bookingId: effectivBookingId,
      roomId: roomIdNum,
      guestName: form.guestName,
      guestPassport: form.guestPassport,
      guestOrigin: form.guestOrigin,
      guestContact: form.guestContact,
      guestEmergencyContactName: form.guestEmergencyContactName,
      guestEmergencyContactNo: form.guestEmergencyContactNo,
      totalGuest: form.totalGuest,
      stayDuration: form.stayDuration,
      bookingDate: new Date(form.bookingDate).toISOString(),
      guestCheckIn: new Date(form.guestCheckIn).toISOString(),
      guestCheckOut: new Date(form.guestCheckOut).toISOString(),
      remarks: form.remarks,
      deposit: form.deposit,
      payment: form.payment,
    }
    saveMutation.mutate(req)
  }

  const status = booking?.active === false ? 'Void' : room?.roomStatus ?? 'Open'

  const statusColors: Record<string, string> = {
    Open: '#76e600', Booked: '#ffea00', Occupied: '#ff1744',
    Housekeeping: '#d500f9', Maintenance: '#ff7929', Void: '#505050',
  }

  return (
    <div>
      {/* Header bar */}
      <div style={{
        background: statusColors[status] ?? '#505050',
        padding: '8px 16px',
        borderRadius: 6,
        marginBottom: 16,
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
      }}>
        <span style={{ fontWeight: 700, fontSize: 16, color: ['Occupied', 'Housekeeping', 'Maintenance'].includes(status) ? '#fff' : '#000' }}>
          Booking No: {effectivBookingId ? effectivBookingId.toString().padStart(6, '0') : 'New'}
        </span>
        <span style={{ fontWeight: 700, color: ['Occupied', 'Housekeeping', 'Maintenance'].includes(status) ? '#fff' : '#000' }}>
          {status}
        </span>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 260px', gap: 16 }}>
        {/* Left column */}
        <div>
          {/* Booking Details */}
          <div className="card" style={{ marginBottom: 12 }}>
            <h3 style={{ marginBottom: 12, fontSize: 14 }}>Booking Details</h3>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
              <div>
                <label>Booking Date & Time</label>
                <input type="datetime-local" value={form.bookingDate}
                  onChange={e => setForm(f => ({ ...f, bookingDate: e.target.value }))} />
              </div>
              <div>
                <label>Total Guests</label>
                <select value={form.totalGuest}
                  onChange={e => setForm(f => ({ ...f, totalGuest: +e.target.value }))}>
                  {[1,2,3,4,5,6].map(n => <option key={n}>{n}</option>)}
                </select>
              </div>
              <div>
                <label>Length of Stay (Nights)</label>
                <select value={form.stayDuration}
                  onChange={e => setForm(f => ({ ...f, stayDuration: +e.target.value }))}>
                  {[1,2,3,4,5,6,7,8,9,10].map(n => <option key={n}>{n}</option>)}
                </select>
              </div>
              <div>
                <label>Check-In Date & Time</label>
                <input type="datetime-local" value={form.guestCheckIn}
                  onChange={e => setForm(f => ({ ...f, guestCheckIn: e.target.value }))} />
              </div>
              <div>
                <label>Check-Out Date & Time</label>
                <input type="datetime-local" value={form.guestCheckOut}
                  onChange={e => setForm(f => ({ ...f, guestCheckOut: e.target.value }))} />
              </div>
            </div>
          </div>

          {/* Guest Details */}
          <div className="card" style={{ marginBottom: 12 }}>
            <h3 style={{ marginBottom: 12, fontSize: 14 }}>Guest Details</h3>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
              <div>
                <label>Name *</label>
                <input value={form.guestName} onChange={e => setForm(f => ({ ...f, guestName: e.target.value }))} placeholder="Full name" />
              </div>
              <div>
                <label>Passport / IC No *</label>
                <input value={form.guestPassport} onChange={e => setForm(f => ({ ...f, guestPassport: e.target.value }))} />
              </div>
              <div>
                <label>Country / Origin</label>
                <input value={form.guestOrigin} onChange={e => setForm(f => ({ ...f, guestOrigin: e.target.value }))} />
              </div>
              <div>
                <label>Contact No</label>
                <input value={form.guestContact} onChange={e => setForm(f => ({ ...f, guestContact: e.target.value }))} />
              </div>
            </div>
          </div>

          {/* Remarks */}
          <div className="card">
            <label>Remarks</label>
            <textarea value={form.remarks} rows={3}
              onChange={e => setForm(f => ({ ...f, remarks: e.target.value }))} />
          </div>
        </div>

        {/* Right column — Room + Pricing */}
        <div>
          <div className="card" style={{ marginBottom: 12 }}>
            <h3 style={{ marginBottom: 12, fontSize: 14 }}>Room Details</h3>
            <table style={{ width: '100%', fontSize: 13 }}>
              <tbody>
                <tr><td style={{ color: 'var(--color-text-dim)' }}>Room No</td><td>{room?.roomShortName ?? '-'}</td></tr>
                <tr><td style={{ color: 'var(--color-text-dim)' }}>Type</td><td>{room?.roomType ?? '-'}</td></tr>
                <tr><td style={{ color: 'var(--color-text-dim)' }}>Location</td><td>{room?.roomLocation ?? '-'}</td></tr>
                <tr><td style={{ color: 'var(--color-text-dim)' }}>Rate (MYR)</td><td>{room?.roomPrice?.toFixed(2) ?? '0.00'}</td></tr>
                <tr><td style={{ color: 'var(--color-text-dim)' }}>Sub Total</td><td>{subTotal.toFixed(2)}</td></tr>
              </tbody>
            </table>
          </div>

          <div className="card" style={{ marginBottom: 12 }}>
            <h3 style={{ marginBottom: 12, fontSize: 14 }}>Payment</h3>
            <div style={{ marginBottom: 8 }}>
              <label>Deposit (MYR)</label>
              <input type="number" step="0.01" value={form.deposit}
                onChange={e => setForm(f => ({ ...f, deposit: +e.target.value }))} />
            </div>
            <div style={{ marginBottom: 8 }}>
              <label>Total Due (MYR)</label>
              <input readOnly value={totalDue.toFixed(2)} style={{ background: '#404040' }} />
            </div>
            <div style={{ marginBottom: 8 }}>
              <label>Payment (MYR)</label>
              <input type="number" step="0.01" value={form.payment}
                onChange={e => setForm(f => ({ ...f, payment: +e.target.value }))} />
            </div>
            {status === 'Occupied' && (
              <div style={{ marginBottom: 8 }}>
                <label>Refund (MYR)</label>
                <input type="number" step="0.01" value={form.refund}
                  onChange={e => setForm(f => ({ ...f, refund: +e.target.value }))} />
              </div>
            )}
          </div>

          {/* Actions */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <button className="btn-primary" onClick={handleSave} disabled={saveMutation.isPending || status === 'Housekeeping'}>
              💾 Save
            </button>
            {status === 'Booked' && (
              <button className="btn-success" onClick={() => checkInMutation.mutate()} disabled={checkInMutation.isPending}>
                ✅ Check-In
              </button>
            )}
            {status === 'Occupied' && (
              <button className="btn-danger" onClick={() => checkOutMutation.mutate()} disabled={checkOutMutation.isPending}>
                🚪 Check-Out
              </button>
            )}
            {bookingIdNum && (
              <>
                <a href={bookingApi.temporaryReceiptUrl(bookingIdNum)} target="_blank" rel="noreferrer">
                  <button className="btn-ghost" style={{ width: '100%' }}>🧾 Temp Receipt (BR-21)</button>
                </a>
                <a href={bookingApi.officialReceiptUrl(bookingIdNum)} target="_blank" rel="noreferrer">
                  <button className="btn-ghost" style={{ width: '100%' }}>📄 Official Receipt (BR-22)</button>
                </a>
              </>
            )}
            <button className="btn-ghost" onClick={() => navigate('/dashboard')}>← Back</button>
          </div>
        </div>
      </div>
    </div>
  )
}