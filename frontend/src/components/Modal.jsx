import * as Dialog from '@radix-ui/react-dialog'
import './Modal.css'

export default function Modal({
  open,
  onOpenChange,
  title,
  description,
  children,
}) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="modal-overlay" />
        <Dialog.Content className="modal">
          <Dialog.Title className="modal__title">{title}</Dialog.Title>
          {description ? (
            <Dialog.Description className="modal__description">
              {description}
            </Dialog.Description>
          ) : (
            <Dialog.Description className="sr-only">{title}</Dialog.Description>
          )}
          {children}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
