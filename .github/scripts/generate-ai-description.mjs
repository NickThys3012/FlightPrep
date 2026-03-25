// Calls GitHub Models (gpt-4o-mini) to generate a Dutch release note description.
// Falls back to PR body or title if the API is unavailable.
// Output is written to /tmp/ai_description.txt

const title  = process.env.PR_TITLE        || '';
const body   = (process.env.PR_BODY        || '').trim();
const labels = process.env.PR_LABELS       || '[]';
const files  = process.env.CHANGED_FILES   || '';
const token  = process.env.GITHUB_TOKEN    || '';

const prompt = [
  'Je bent technisch schrijver voor FlightPrep, een Blazor-webapplicatie voor voorbereiding van ballonvluchten.',
  '',
  'Genereer een beknopte release note in het Nederlands (max 3 zinnen) voor onderstaande pull request.',
  'Wees concreet: noem wat er nieuw/gewijzigd is en wat het voordeel is voor de piloot/gebruiker.',
  'Schrijf ALLEEN de release note tekst, zonder titels, bullet points of opmaak.',
  '',
  `PR Titel: ${title}`,
  `PR Beschrijving: ${body || '(geen)'}`,
  `Labels: ${labels}`,
  `Gewijzigde bestanden: ${files || '(onbekend)'}`,
].join('\n');

let description = body || title;

try {
  const res = await fetch('https://models.inference.ai.azure.com/chat/completions', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      model: 'gpt-4o-mini',
      messages: [{ role: 'user', content: prompt }],
      max_tokens: 250,
      temperature: 0.6,
    }),
  });

  if (res.ok) {
    const data = await res.json();
    description = data.choices?.[0]?.message?.content?.trim() || description;
    console.error(`AI generated description (${description.length} chars)`);
  } else {
    const err = await res.text();
    console.error(`GitHub Models API ${res.status} — using fallback. Response: ${err}`);
  }
} catch (e) {
  console.error(`AI call failed: ${e.message} — using fallback`);
}

const { writeFileSync } = await import('fs');
writeFileSync('/tmp/ai_description.txt', description);
console.log('Description written to /tmp/ai_description.txt');
