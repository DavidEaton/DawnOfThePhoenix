"""Generate local SVG payment QR codes; no external QR service or runtime dependency.

Install qrcode==8.2 in a development virtual environment, then run this script.
Use --check to verify the committed images match their intended destinations.
"""

import argparse
from io import BytesIO
from pathlib import Path

import qrcode
from qrcode.image.svg import SvgPathFillImage


DESTINATIONS = {
    "cash-app": "https://cash.app/$22PhoenixRising888",
    "paypal": "https://www.paypal.com/qrcodes/p2pqrc/XBDN5S58CNNYQ",
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    directory = Path(__file__).resolve().parents[1] / "DawnOfThePhoenix.Web/wwwroot/assets/tips"
    directory.mkdir(parents=True, exist_ok=True)
    for name, destination in DESTINATIONS.items():
        qr = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=4)
        qr.add_data(destination)
        qr.make(fit=True)
        buffer = BytesIO()
        qr.make_image(image_factory=SvgPathFillImage).save(buffer)
        output = buffer.getvalue() + b"\n"
        target = directory / f"{name}.svg"
        if args.check:
            if not target.exists() or target.read_bytes() != output:
                raise SystemExit(f"QR code needs regeneration: {target}")
        else:
            target.write_bytes(output)
        print(f"{'Verified' if args.check else 'Generated'} {name}: {destination}")


if __name__ == "__main__":
    main()
