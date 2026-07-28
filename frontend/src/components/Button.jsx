import './Button.css'

const VARIANT_CLASS = {
  primary: 'button button--primary',
  secondary: 'button button--secondary',
  destructive: 'button button--destructive',
}

export default function Button({
  variant = 'primary',
  type = 'button',
  className,
  children,
  ...rest
}) {
  const variantClass = VARIANT_CLASS[variant] ?? VARIANT_CLASS.primary
  const classes = className ? `${variantClass} ${className}` : variantClass

  return (
    <button type={type} className={classes} {...rest}>
      {children}
    </button>
  )
}
