export function loadScript(src, onLoad, onError) {
  const script = document.createElement('script')
  script.src = src
  script.async = true
  script.onload = onLoad
  script.onerror = onError
  document.body.appendChild(script)
}
