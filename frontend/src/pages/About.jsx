import './About.css'

export default function About() {
  return (
    <div className="about">
      <h1 className="about__title">About Fake Barbershop</h1>

      <section className="about__section">
        <h2 className="about__heading">Location</h2>
        <p>123 Main Street, Springfield</p>
      </section>

      <section className="about__section">
        <h2 className="about__heading">Phone</h2>
        <p>(555) 010-2020</p>
      </section>

      <section className="about__section">
        <h2 className="about__heading">Hours</h2>
        <p>Mon–Fri, 9:00 AM – 4:30 PM</p>
      </section>

      <section className="about__section">
        <h2 className="about__heading">Our Barbers</h2>
        <p>Manny, Dana, and Theo</p>
      </section>
    </div>
  )
}
