import { initializeBooking } from './booking.js';
import { verifyCheckoutReturn } from './checkout.js';
import { initializeDialog } from './dialog.js';
import { initializeLayout } from './layout.js';
import { initializeReadings } from './readings.js';
import { initializeTips } from './tips.js';

initializeDialog();
initializeLayout();
initializeBooking();
initializeReadings();
initializeTips();
verifyCheckoutReturn();
