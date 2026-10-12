const explanations: Record<string, { title: string; detail: string }> = {
  SUITABLE: { title: 'Within the reviewed limits', detail: 'The available evidence meets this activity’s reviewed profile. Check local advice and supervision before deciding to proceed.' },
  CAUTION: { title: 'Extra care is needed', detail: 'At least one condition has entered a reviewed caution band. Read the factors below and discuss the conditions with your activity operator.' },
  UNSUITABLE: { title: 'Outside the reviewed limits', detail: 'At least one condition exceeds this activity’s reviewed maximum. Consider another activity or time and check local advice.' },
  UNKNOWN: { title: 'There is not enough reliable evidence', detail: 'Missing or stale evidence prevents a positive assessment. Obtain current information before making a decision.' },
}

export default function MarineResultExplanation({ status }: { status: string }) {
  const explanation = explanations[status] ?? { title: 'Assessment result unavailable', detail: 'This result cannot be interpreted reliably. Check saved evidence or request current conditions.' }
  return <div className="mt-5 border-t border-coast-line/70 pt-4">
    <h4 className="font-display text-xl tracking-[-0.025em]">{explanation.title}</h4>
    <p className="mt-2 max-w-3xl text-sm leading-6 text-coast-ink">{explanation.detail}</p>
  </div>
}
