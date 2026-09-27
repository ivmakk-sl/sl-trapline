// Checked only by npm run typecheck: the fixture of the setData data, which the C# tests build, fits
// the page types.
import type { PageData } from '../../src/Web/page/types';
import data from '../fixtures/data.json';

const checked: PageData = data;
export default checked;
