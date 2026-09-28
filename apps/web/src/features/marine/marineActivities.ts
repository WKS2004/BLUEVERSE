// Frozen G00 reference identities for Member 1's coastal activities until the
// canonical experience catalogue exists. The marine-safety service seeds these
// exact GUIDs (`docs/v1/g00/member-2-marine-safety-decisions.md`); the UI uses
// them only to target the existing marine operations and adds no catalogue
// features of its own.
export type MarineActivityReference = {
  id: string
  name: string
  activityType: string
}

export const MARINE_ACTIVITY_REFERENCES: MarineActivityReference[] = [
  { id: '33333333-3333-3333-3333-333333333301', name: 'Surfing', activityType: 'Surfing' },
  { id: '33333333-3333-3333-3333-333333333302', name: 'Snorkeling', activityType: 'Snorkeling' },
  { id: '33333333-3333-3333-3333-333333333303', name: 'Scuba Diving', activityType: 'Diving' },
  { id: '33333333-3333-3333-3333-333333333304', name: 'Whale and Dolphin Watching', activityType: 'BoatTour' },
  { id: '33333333-3333-3333-3333-333333333305', name: 'Coastal Boat Tour', activityType: 'BoatTour' },
]
