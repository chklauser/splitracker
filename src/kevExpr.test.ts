import {parse} from './kevExpr';
import {Point} from "./PointDisplay";

test('empty', () => {
  expect(parse('', Point.Exhausted)).toEqual({channeled: 0, exhausted: 0, consumed: 0});
  expect(parse('    ', Point.Exhausted)).toEqual({channeled: 0, exhausted: 0, consumed: 0});
  expect(parse('qPk.,:/?', Point.Exhausted)).toEqual({channeled: 0, exhausted: 0, consumed: 0});
});

test('untyped', () => {
  expect(parse('15', Point.Exhausted)).toEqual({channeled: 0, exhausted: 15, consumed: 0});
  expect(parse('7', Point.Channeled)).toEqual({channeled: 7, exhausted: 0, consumed: 0});
  expect(parse('3', Point.Consumed)).toEqual({channeled: 0, exhausted: 0, consumed: 3});
});

test('consecutive values are added', () => {
  expect(parse('1 2 3', Point.Exhausted)).toEqual({channeled: 0, exhausted: 6, consumed: 0});
});

test('factor applies to consecutive values', () => {
  expect(parse('-1 2 3 +7 11', Point.Exhausted)).toEqual({
    channeled: 0,
    exhausted: -(1 + 2 + 3) + (7 + 11),
    consumed: 0
  });
});

test('type applies to consecutive values', () => {
  expect(parse('K1 2 3', Point.Channeled)).toEqual({channeled: 1 + 2 + 3, exhausted: 0, consumed: 0});
  expect(parse('E1 2 3 ', Point.Exhausted)).toEqual({channeled: 0, exhausted: 1 + 2 + 3, consumed: 0});
  expect(parse('V1 2 3 ', Point.Consumed)).toEqual({channeled: 0, exhausted: 0, consumed: 1 + 2 + 3});
  expect(parse('k1 2 3', Point.Channeled)).toEqual({channeled: 1 + 2 + 3, exhausted: 0, consumed: 0});
  expect(parse('e1 2 3 ', Point.Exhausted)).toEqual({channeled: 0, exhausted: 1 + 2 + 3, consumed: 0});
  expect(parse('v1 2 3 ', Point.Consumed)).toEqual({channeled: 0, exhausted: 0, consumed: 1 + 2 + 3});
});

test('factor applies to all types', () => {
  expect(parse('K-1 2 v3 +7 11 ', Point.Channeled)).toEqual({
    channeled: -(1 + 2),
    exhausted: 0,
    consumed: -(3) + (7 + 11)
  });
  expect(parse('e-1 2 V3 +7 11 ', Point.Exhausted)).toEqual({
    channeled: 0,
    exhausted: -(1 + 2),
    consumed: -(3) + (7 + 11)
  });
});