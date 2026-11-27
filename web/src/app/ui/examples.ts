/**
 * Sample data for the "Fill with an example" panels. Every value here is MADE UP for demos: policy numbers, people,
 * addresses, amounts and receiver URLs (example.com / .org / .net are reserved for documentation). One file, so a
 * reviewer can see at a glance that no example pretends to be real data.
 */

/** A dated value is written as "N days ago" so an example never drifts outside the API's one-year window. */
export function daysAgo(n: number, now = new Date()): string {
  const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() - n);
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${m}-${day}`;
}

/** Four random digits, so an example that must be unique (an email) can be submitted more than once. */
function tag4(): string {
  return String(Math.floor(1000 + Math.random() * 9000));
}

// ---------- Report an incident ----------

export interface IncidentExample {
  id: string;
  market: string;
  line: 'Motor' | 'Property';
  title: string;
  daysAgo: number;
  location: string;
  description: string;
}

const INCIDENTS: [string, 'Motor' | 'Property', string, number, string, string][] = [
  [
    'SG',
    'Motor',
    'Rear-ended at a junction',
    2,
    'Junction of Bukit Timah Road and Dunearn Road',
    'Stopped at a red light when a delivery van hit the rear of my car. The rear bumper and boot lid are dented and the parking sensors no longer work. The van driver gave me his details and admitted fault.',
  ],
  [
    'SG',
    'Motor',
    'Side swipe in a car park',
    5,
    'Basement 2 car park, Tampines Mall',
    'While I was reversing out of a lot, another car scraped the left side of my car. Both passenger-side doors are scratched and the wing mirror is cracked. The car park CCTV may have recorded it.',
  ],
  [
    'SG',
    'Motor',
    'Windscreen cracked by road debris',
    9,
    'Pan Island Expressway towards Changi, near exit 8',
    'A stone thrown up by a lorry hit the windscreen. The crack has spread across the driver side and the car is not safe to drive. No other damage.',
  ],
  [
    'SG',
    'Property',
    'Water leak from the flat above',
    3,
    'Blk 512 Bishan Street 13',
    'Water has leaked through the kitchen ceiling from the unit above for two days. The ceiling board is stained and sagging, and the built-in cabinets below have swollen. The town council has been told.',
  ],
  [
    'SG',
    'Property',
    'Burst pipe in the bathroom',
    6,
    'Toa Payoh Lorong 1',
    'A pipe behind the bathroom wall burst overnight. Water spread into the bedroom and damaged the laminate floor and a wardrobe. A plumber has capped the pipe.',
  ],
  [
    'SG',
    'Property',
    'Kitchen fire from the stove',
    12,
    'Jurong West Street 91',
    'A pan caught fire on the stove. It was put out with an extinguisher, but the hood, upper cabinets and kitchen wall are smoke and fire damaged. Nobody was hurt.',
  ],
  [
    'MY',
    'Motor',
    'Motorcycle hit my door at the lights',
    1,
    'Jalan Tun Razak, Kuala Lumpur',
    'A motorcycle filtering between lanes hit my front left door while I was stopped at the lights. The door is dented and the paint is scraped. A police report has been made.',
  ],
  [
    'MY',
    'Motor',
    'Flood damage in heavy rain',
    4,
    'Taman Sri Muda, Shah Alam',
    'Heavy rain flooded the road outside my house and the water rose above the door sills. The engine will not start and the carpets are soaked.',
  ],
  [
    'MY',
    'Motor',
    'Collision on the highway',
    8,
    'PLUS Highway near the Rawang exit',
    'The car in front braked suddenly and I could not stop in time. The front bumper, bonnet and radiator are damaged. The car was towed to a panel workshop.',
  ],
  [
    'MY',
    'Property',
    'Roof leak after a storm',
    2,
    'Jalan SS2/24, Petaling Jaya',
    'Strong winds lifted several roof tiles and rain came into the upstairs bedroom. The ceiling has a large water stain and the mattress and carpet are wet.',
  ],
  [
    'MY',
    'Property',
    'Break-in through the back door',
    5,
    'Taman Tun Dr Ismail, Kuala Lumpur',
    'Someone forced the back door while we were away for the weekend. A laptop, a television and jewellery were taken and the door frame is broken. A police report was made the same day.',
  ],
  [
    'MY',
    'Property',
    'Lightning damaged appliances',
    10,
    'Bandar Baru Bangi, Selangor',
    'Lightning struck near the house during a storm. The television, the refrigerator and the router stopped working and the main switch tripped.',
  ],
  [
    'TH',
    'Motor',
    'Rear-ended in slow traffic',
    2,
    'Sukhumvit Road near Soi 24, Bangkok',
    'A taxi hit the back of my car in stop-and-go traffic. The rear bumper is cracked and the boot does not close. We exchanged insurer details at the scene.',
  ],
  [
    'TH',
    'Motor',
    'Scooter broke the side mirror',
    6,
    'Nimmanhaemin Road, Chiang Mai',
    'A scooter passing on the left clipped the side mirror and scratched the front door. The mirror housing is broken and hanging off.',
  ],
  [
    'TH',
    'Motor',
    'Pothole burst a tyre',
    11,
    'Highway 4 near Hua Hin',
    'I hit a deep pothole at night. The front left tyre burst and the alloy wheel is bent. The car was towed to the nearest garage.',
  ],
  [
    'TH',
    'Property',
    'Ground floor flooded',
    4,
    'Bang Na, Bangkok',
    'Heavy rain flooded the ground floor to about 30 cm. The sofa, furniture and floor tiles in the living room are damaged.',
  ],
  [
    'TH',
    'Property',
    'Air-conditioner leak brought the ceiling down',
    7,
    'Condominium unit, Ratchada, Bangkok',
    'The air-conditioner drain leaked into the ceiling. Part of the gypsum ceiling collapsed onto a desk and a computer underneath.',
  ],
  [
    'TH',
    'Property',
    'Electrical fire in the storeroom',
    15,
    'Shophouse in Phuket Old Town',
    'A short circuit started a small fire in the storeroom. Stock and shelving were burnt and the walls are smoke damaged. The fire brigade attended.',
  ],
  [
    'HK',
    'Motor',
    'Minibus hit my parked car',
    1,
    'Nathan Road, Mong Kok',
    'My car was parked legally when a minibus pulling out hit the rear right side. The tail light is smashed and the panel is dented. The police attended and took statements.',
  ],
  [
    'HK',
    'Motor',
    'Collision in the tunnel',
    5,
    'Cross-Harbour Tunnel, Kowloon side',
    'Traffic stopped suddenly in the tunnel and the car behind hit mine. The rear bumper and exhaust are damaged.',
  ],
  [
    'HK',
    'Motor',
    'Key scratch in a car park',
    13,
    'Festival Walk car park, Kowloon Tong',
    'I came back to a long key scratch along both doors on the driver side. Nothing was taken.',
  ],
  [
    'HK',
    'Property',
    'Typhoon broke a window',
    3,
    'Pok Fu Lam Road',
    'During typhoon signal 8 a living room window shattered. Rain soaked the curtains, the sofa and the wooden floor.',
  ],
  [
    'HK',
    'Property',
    'Seepage from the bathroom above',
    8,
    'Tai Koo Shing, Quarry Bay',
    'Water has seeped through the bathroom ceiling for a week. The paint is peeling and the ceiling light shorted. Building management has inspected it.',
  ],
  [
    'HK',
    'Property',
    'Burglary while travelling',
    20,
    'Village house, Sai Kung',
    'The house was broken into while we were overseas. A camera, two watches and cash were taken and a window grille was cut. Reported to the police on our return.',
  ],
  [
    'ID',
    'Motor',
    'Hit by a car changing lanes',
    2,
    'Jl. Jenderal Sudirman, Jakarta',
    'A car changed lanes without signalling and hit the front right of my car. The headlamp is broken and the fender is dented. The other driver agreed to settle through insurance.',
  ],
  [
    'ID',
    'Motor',
    'Flood water entered the engine',
    5,
    'Kelapa Gading, North Jakarta',
    'The street flooded after heavy rain and the car stalled in the water. It will not start and the workshop says water entered the engine.',
  ],
  [
    'ID',
    'Motor',
    'Rear-ended at a toll gate',
    9,
    'Cikarang Utama toll gate, Jakarta-Cikampek toll road',
    'A truck behind me did not stop in time in the toll gate queue. The rear bumper and boot are damaged.',
  ],
  [
    'ID',
    'Property',
    'Carport roof collapsed in heavy rain',
    3,
    'Bumi Serpong Damai, Tangerang',
    'Part of the carport roof collapsed after heavy rain onto the front terrace. The roof frame and tiles need replacing.',
  ],
  [
    'ID',
    'Property',
    'Gas hose fire in the kitchen',
    9,
    'Jl. Dago, Bandung',
    'A gas stove hose leaked and caught fire. The kitchen cabinets and ceiling are burnt; neighbours put the fire out.',
  ],
  [
    'ID',
    'Property',
    'Theft from the shop',
    14,
    'Pasar Baru, Jakarta',
    'Thieves broke the padlock of my shop at night and took mobile phones and accessories from the display cases.',
  ],
  [
    'PH',
    'Motor',
    'Jeepney swerved into my lane',
    1,
    'EDSA near Ortigas, Pasig',
    'A jeepney swerved into my lane and hit the left side of my car. The rear door and quarter panel are dented. A traffic enforcer made a report at the scene.',
  ],
  [
    'PH',
    'Motor',
    'Typhoon flood in the street',
    6,
    'Espana Boulevard, Manila',
    'During the typhoon the street flooded to knee height while the car was parked. Water entered the cabin and the electronics no longer work.',
  ],
  [
    'PH',
    'Motor',
    'Reversed into a post',
    12,
    'SM Megamall car park, Mandaluyong',
    'I reversed into a concrete post while parking. The rear bumper is cracked and a parking sensor is broken. Own damage only.',
  ],
  [
    'PH',
    'Property',
    'Typhoon tore off roof sheets',
    4,
    'Barangay Pinagsama, Taguig',
    'Strong winds during the typhoon tore off several roof sheets. Rain entered two bedrooms and damaged the ceiling, a bed and clothes.',
  ],
  [
    'PH',
    'Property',
    'River overflow flooded the ground floor',
    7,
    'Marikina Heights, Marikina',
    'The river overflowed and the ground floor flooded to about 60 cm. The refrigerator, washing machine and sofa are damaged.',
  ],
  [
    'PH',
    'Property',
    'Extension cord fire in the bedroom',
    18,
    'Lahug, Cebu City',
    'An extension cord overheated and started a fire in the bedroom. The bed, curtains and a wardrobe are burnt and the walls are smoke damaged.',
  ],
];

export const INCIDENT_EXAMPLES: readonly IncidentExample[] = INCIDENTS.map(
  ([market, line, title, ago, location, description], i) => ({
    id: `incident-${i}`,
    market,
    line,
    title,
    daysAgo: ago,
    location,
    description,
  }),
);

/** A sample policy number in a made-up shape, e.g. `SAMPLE-SG-MTR-40012`. */
export function samplePolicy(e: IncidentExample): string {
  return `SAMPLE-${e.market}-${e.line === 'Motor' ? 'MTR' : 'PRP'}-${40000 + Number(e.id.split('-')[1]) * 7}`;
}

/**
 * The incident examples worth showing for what the form already says: the chosen market and line narrow the list;
 * with nothing chosen, one per market (alternating Motor and Property) so every market is one click away.
 */
export function incidentExamplesFor(market: string, line: string): IncidentExample[] {
  if (!market && !line)
    return [...new Set(INCIDENT_EXAMPLES.map((e) => e.market))].map((m, i) =>
      INCIDENT_EXAMPLES.find((e) => e.market === m && e.line === (i % 2 ? 'Property' : 'Motor'))!,
    );
  return INCIDENT_EXAMPLES.filter((e) => (!market || e.market === market) && (!line || e.line === line));
}

// ---------- Claim actions (officer) and the claimant's answer ----------

export interface TextExample {
  id: string;
  title: string;
  text: string;
}

function texts(rows: [string, string][]): TextExample[] {
  return rows.map(([title, text], i) => ({ id: `t-${i}`, title, text }));
}

export const REQUEST_INFO_EXAMPLES = texts([
  [
    'Photos and a repair quote',
    'Please upload clear photos of the damage and the repair quote from your workshop or contractor.',
  ],
  [
    'Police or incident report',
    'Please send the police report number (or the building management incident report) and the date it was made.',
  ],
  [
    'Proof of ownership',
    'Please send receipts or other proof of ownership for the items you are claiming, with their purchase dates.',
  ],
]);

export const APPROVE_EXAMPLES = texts([
  [
    'Covered, quote verified',
    'Covered under the policy terms. The repair quote was checked against the photos and the workshop invoice.',
  ],
  [
    'Covered, excess applied',
    'Covered under the policy terms. The policy excess has been deducted from the approved amount.',
  ],
  [
    'Covered after the answers',
    "Covered under the policy terms. The claimant's answers and documents confirm the loss and its date.",
  ],
]);

export const REJECT_EXAMPLES = texts([
  ['Before the policy period', 'The damage happened before the policy period started, so it is not covered.'],
  ['Excluded cause', 'The cause of the loss (wear and tear) is excluded under the policy terms.'],
  ['No documents received', 'We asked for the documents needed to assess this claim and did not receive them.'],
]);

export const ANSWER_EXAMPLES = texts([
  [
    'Photos and quote sent',
    'I have emailed six photos of the damage and the workshop quote to the claims inbox this morning.',
  ],
  [
    'Police report number',
    'The police report number is SAMPLE-RPT-20418, made at the station on the same day as the incident.',
  ],
  [
    'Receipts attached',
    'I found the receipts for the laptop and the television; copies are in the email I sent today.',
  ],
]);

/** Reserve amounts in major units by currency (made-up round figures for a small, typical and large claim). */
const RESERVES: Record<string, [number, number, number]> = {
  SGD: [1800, 6500, 25000],
  MYR: [6000, 22000, 85000],
  THB: [45000, 170000, 650000],
  HKD: [10000, 38000, 145000],
  IDR: [20000000, 75000000, 290000000],
  PHP: [75000, 280000, 1050000],
};

export interface AmountExample {
  id: string;
  title: string;
  amount: string;
}

export function reserveExamples(currency: string): AmountExample[] {
  const [small, typical, large] = RESERVES[currency] ?? RESERVES['SGD'];
  return [
    { id: 'r-small', title: 'Minor repair', amount: small.toFixed(2) },
    { id: 'r-typical', title: 'Typical claim', amount: typical.toFixed(2) },
    { id: 'r-large', title: 'Major loss', amount: large.toFixed(2) },
  ];
}

// ---------- Webhooks, customers, users ----------

export interface WebhookExample {
  id: string;
  name: string;
  url: string;
  events: string[];
}

export const WEBHOOK_EXAMPLES: readonly WebhookExample[] = [
  { id: 'w-billing', name: 'Billing sync', url: 'https://example.com/hooks/fugue-billing', events: ['customer.*'] },
  {
    id: 'w-crm',
    name: 'CRM mirror',
    url: 'https://example.org/webhooks/crm',
    events: ['customer.created.v1', 'customer.updated.v1'],
  },
  { id: 'w-archive', name: 'Event archive', url: 'https://example.net/fugue/events', events: ['*'] },
];

export interface PersonExample {
  id: string;
  name: string;
  email: string;
  phone: string;
}

/** Customers with a fresh email tag each time the list is built, so the same example can be created twice. */
export function customerExamples(): PersonExample[] {
  const t = tag4();
  return [
    { id: 'c-sg', name: 'Tan Wei Ling', email: `weiling.tan+${t}@example.com`, phone: '+6581234567' },
    { id: 'c-my', name: 'Nur Aisyah binti Rahman', email: `aisyah.rahman+${t}@example.com`, phone: '+60123456789' },
    { id: 'c-ph', name: 'Miguel Santos', email: `miguel.santos+${t}@example.com`, phone: '' },
  ];
}

export interface UserExample {
  id: string;
  title: string;
  displayName: string;
  email: string;
  roles: string[];
}

/** Roles use the codes the API seeds (ADMIN, MANAGER, SELF_SERVICE, STAFF); the drawer drops any it does not list. */
export function userExamples(): UserExample[] {
  const t = tag4();
  return [
    {
      id: 'u-officer',
      title: 'Claims officer',
      displayName: 'Priya Nair',
      email: `priya.nair+${t}@example.com`,
      roles: ['STAFF'],
    },
    {
      id: 'u-manager',
      title: 'Claims manager',
      displayName: 'Daniel Lim',
      email: `daniel.lim+${t}@example.com`,
      roles: ['MANAGER'],
    },
    {
      id: 'u-claimant',
      title: 'Self-service claimant',
      displayName: 'Siti Hartono',
      email: `siti.hartono+${t}@example.com`,
      roles: ['SELF_SERVICE'],
    },
  ];
}

const WORDS = ['amber', 'harbour', 'lantern', 'monsoon', 'orchid', 'pepper', 'river', 'saffron', 'teak', 'willow'];

/** A readable random sample password (four words and two digits, 20+ characters), shown once next to the field. */
export function samplePassword(): string {
  const n = crypto.getRandomValues(new Uint32Array(5));
  const words = [...n.slice(0, 4)].map((v) => WORDS[v % WORDS.length]);
  return `${words.join('-')}-${10 + (n[4] % 90)}`;
}
