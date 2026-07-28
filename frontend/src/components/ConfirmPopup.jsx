import Modal from './Modal'
import Button from './Button'
import './ConfirmPopup.css'

export default function ConfirmPopup({
  open,
  onOpenChange,
  title,
  message,
  destructive = false,
  onConfirm,
  confirmLabel = 'Confirm',
  cancelLabel = 'Go Back',
}) {
  const handleConfirm = () => {
    onConfirm?.()
    onOpenChange(false)
  }

  return (
    <Modal
      open={open}
      onOpenChange={onOpenChange}
      title={title}
      description={message}
    >
      <div className="confirm-popup__actions">
        <Button variant="secondary" onClick={() => onOpenChange(false)}>
          {cancelLabel}
        </Button>
        <Button
          variant={destructive ? 'destructive' : 'primary'}
          onClick={handleConfirm}
        >
          {confirmLabel}
        </Button>
      </div>
    </Modal>
  )
}
