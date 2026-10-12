type OperationsHeroProps = {
  id: string
  image: string
  imageAlt: string
  label: string
  title: string
  description: string
  context: string
}

export default function OperationsHero({ id, image, imageAlt, label, title, description, context }: OperationsHeroProps) {
  return <section aria-labelledby={id} className="overflow-hidden rounded-[1.75rem] border border-coast-line bg-coast-pearl shadow-sm">
    <div className="relative h-48 overflow-hidden bg-coast-sage sm:h-60 lg:h-64">
      <img alt={imageAlt} className="h-full w-full object-cover object-[70%_55%]" decoding="async" src={image} />
      <p className="absolute bottom-3 left-3 rounded-full bg-coast-pearl/95 px-3 py-2 text-[9px] font-extrabold tracking-[0.14em] text-coast-deep sm:bottom-4 sm:left-4">BLUEVERSE · COASTAL OPERATIONS</p>
    </div>
    <div className="p-5 sm:p-8">
      <p className="inline-flex rounded-full border border-coast-line px-3 py-1.5 text-[10px] font-extrabold tracking-[0.16em] text-coast-blue">{label}</p>
      <h2 className="mt-4 max-w-xl font-display text-2xl leading-tight font-bold tracking-[-0.035em] text-coast-ink sm:text-3xl" id={id}>{title}</h2>
      <p className="mt-3 max-w-xl text-sm leading-6 text-coast-muted">{description}</p>
      <p className="mt-5 flex items-start gap-2 border-t border-coast-line pt-4 text-xs leading-5 font-semibold text-coast-deep">
        <span aria-hidden="true" className="mt-1.5 size-2 shrink-0 rounded-full bg-coast-teal" />{context}
      </p>
    </div>
  </section>
}
