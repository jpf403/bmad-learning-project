export function loadScript(src, onLoad) {
  const script = document.createElement('script')
  script.src = src
  script.async = true
  script.onload = onLoad
  document.body.appendChild(script)
}
