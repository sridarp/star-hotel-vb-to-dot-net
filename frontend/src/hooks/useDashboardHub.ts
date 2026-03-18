import { useEffect, useRef, useCallback } from 'react'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '../store/authStore'
import type { RoomSummary } from '../types'

type RoomStatusChangedHandler = (roomId: number, status: string, shouldBlink: boolean) => void
type SummaryChangedHandler = (summary: RoomSummary) => void

export function useDashboardHub(
  onRoomStatusChanged: RoomStatusChangedHandler,
  onSummaryChanged: SummaryChangedHandler,
) {
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  const token = useAuthStore(s => s.token)

  const connect = useCallback(async () => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/dashboard', {
        accessTokenFactory: () => token ?? '',
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.on('RoomStatusChanged', (data: { roomId: number; status: string; shouldBlink: boolean }) => {
      onRoomStatusChanged(data.roomId, data.status, data.shouldBlink)
    })

    connection.on('SummaryChanged', (data: RoomSummary) => {
      onSummaryChanged(data)
    })

    connectionRef.current = connection

    try {
      await connection.start()
      await connection.invoke('JoinDashboard', 'main')
    } catch (err) {
      console.warn('SignalR connection failed, falling back to polling', err)
    }
  }, [token, onRoomStatusChanged, onSummaryChanged])

  useEffect(() => {
    connect()
    return () => {
      connectionRef.current?.stop()
    }
  }, [connect])

  return { isConnected: connectionRef.current?.state === signalR.HubConnectionState.Connected }
}