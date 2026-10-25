// Reproducible exact-integer fast path; no clock rate, rounding or locks change.
const fs = require('node:fs'), path = require('node:path');
const file = path.join(__dirname, 'tools/rexglue-source/rexglue-sdk-0.10.0/src/core/clock.cpp');
let text = fs.readFileSync(file, 'utf8');
if (!text.includes('inline uint64_t ScaleGuestTicks(')) {
  const marker = 'std::mutex tick_mutex_;';
  const expressions = [
    ['host_tick_count * guest_tick_ratio_.first / guest_tick_ratio_.second', 'ScaleGuestTicks(host_tick_count, guest_tick_ratio_.first, guest_tick_ratio_.second)'],
    ['host_tick_delta * guest_tick_ratio_.first / guest_tick_ratio_.second', 'ScaleGuestTicks(host_tick_delta, guest_tick_ratio_.first, guest_tick_ratio_.second)']
  ];
  if (text.split(marker).length !== 2 || expressions.some(([before]) => text.split(before).length !== 2))
    throw new Error('Pinned guest clock changed; inspect before patching');
  text = text.replace(marker, marker + `

// Preserve the original uint64_t multiplication (including wraparound) and
// integer rounding. A reduced ratio with denominator one needs no division.
inline uint64_t ScaleGuestTicks(uint64_t ticks, uint64_t numerator, uint64_t denominator) {
  const uint64_t product = ticks * numerator;
  return denominator == 1 ? product : product / denominator;
}`);
  for (const [before, after] of expressions) text = text.replace(before, after);
  fs.writeFileSync(file, text);
}
console.log('Guest clock: exact denominator-one fast path retained.');
