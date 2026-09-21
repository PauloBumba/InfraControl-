// SignalR connection builder placeholder
import * as signalR from "@microsoft/signalr";

export function createHubConnection(hubUrl: string): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .build();
}
