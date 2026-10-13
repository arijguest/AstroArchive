"""Add approximate redshift distances to deep-sky cards of the target information pack.

OpenNGC supplies a redshift for most galaxies. Where the recession velocity is large
enough (cz of at least 1000 km/s) to outweigh a galaxy's own motion, the distance follows
from a flat Lambda-CDM cosmology (H0 = 70 km/s/Mpc, matter density 0.3). The result is stored
as a "Distance" measurement in light-years, marked band="redshift" so the app shows it as
approximate. No distance data from any other catalogue is added.
"""
import math

HUBBLE = 70.0            # km/s/Mpc
MATTER = 0.3
LIGHT_KM_S = 299792.458
LY_PER_MPC = 3261563.78
MIN_REDSHIFT = 1000.0 / LIGHT_KM_S
MAX_REDSHIFT = 2.0


def comoving_mpc(z, steps=400):
    """Comoving distance by Simpson's rule over 1/E(z)."""
    def inverse(x):
        return 1.0 / math.sqrt(MATTER * (1 + x) ** 3 + (1 - MATTER))
    h = z / steps
    total = inverse(0) + inverse(z)
    for i in range(1, steps):
        total += (4 if i % 2 else 2) * inverse(i * h)
    return LIGHT_KM_S / HUBBLE * total * h / 3


def significant(value, digits=3):
    if value <= 0:
        return value
    scale = 10 ** (digits - 1 - int(math.floor(math.log10(value))))
    return round(value * scale) / scale


def apply(cards):
    """Add a Distance measurement to every deep-sky card with a usable redshift; returns the count."""
    added = 0
    for bucket in cards.values():
        for card in bucket.values():
            if card.get('domain') != 'deep_sky':
                continue
            measurements = card.get('measurements') or []
            if any(m['field'] == 'Distance' for m in measurements):
                continue
            redshift = next((m for m in measurements if m['field'] == 'Redshift'), None)
            if redshift is None or not MIN_REDSHIFT <= redshift['value'] <= MAX_REDSHIFT:
                continue
            light_years = significant(comoving_mpc(redshift['value']) * LY_PER_MPC)
            measurements.append({
                'id': redshift['id'].rsplit(':', 1)[0] + ':Distance',
                'object_id': card['id'],
                'field': 'Distance',
                'value': light_years,
                'unit': 'ly',
                'epoch': None,
                'band': 'redshift',
                'uncertainty': None,
                'qualifier': 'Approximate. Calculated from the catalogue redshift z = ' + format(redshift['value'], '.5g') +
                             ' for a flat universe (H0 = 70 km/s/Mpc, matter density 0.3). A galaxy\'s own motion and the '
                             'choice of cosmology can shift this considerably.',
                'citation_id': redshift.get('citation_id'),
            })
            card['measurements'] = measurements
            added += 1
    return added
