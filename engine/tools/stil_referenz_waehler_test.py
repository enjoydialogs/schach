"""Testhilfe fuer `eichung.lade_waehler`: eine Waehler-Fabrik, wie sie ein externer Hook liefert."""

import stil_referenz as s


def fabrik(parameter):
    def waehler(board, policy, seed, halbzug):
        return max(policy, key=lambda z: z[1])[0], s.Merkmal.KEINE

    return waehler
