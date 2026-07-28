import { useId } from 'react'
import './Input.css'

export default function Input({
  label,
  caption,
  error,
  id,
  className,
  ...rest
}) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const captionText = error ?? caption

  const classes = className ? `input ${className}` : 'input'

  return (
    <div className="input-field">
      {label && (
        <label className="input-field__label" htmlFor={inputId}>
          {label}
        </label>
      )}
      <input id={inputId} className={classes} {...rest} />
      {captionText && (
        <span
          className={
            error
              ? 'input-field__caption input-field__caption--error'
              : 'input-field__caption'
          }
        >
          {captionText}
        </span>
      )}
    </div>
  )
}
