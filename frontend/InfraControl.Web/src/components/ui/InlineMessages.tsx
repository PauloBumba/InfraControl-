import { forwardRef, useCallback, useRef } from "react";
import { Messages, type MessagesMessage } from "primereact/messages";

export type InlineMessage = Pick<MessagesMessage, "severity" | "summary" | "detail">;

/** Feedback persistente, junto ao conteúdo que gerou a ação. */
export const InlineMessages = forwardRef<Messages>((_, ref) => (
  <Messages ref={ref} className="uni-inline-messages" />
));

InlineMessages.displayName = "InlineMessages";

export function useInlineMessages() {
  const messagesRef = useRef<Messages>(null);
  const showMessage = useCallback((message: InlineMessage) => {
    messagesRef.current?.clear();
    messagesRef.current?.show({ sticky: true, closable: true, ...message });
  }, []);

  return { messagesRef, showMessage };
}
