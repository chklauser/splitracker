import {Fragment, FunctionComponent} from "react";
import {classSet} from "../ClassSet";
import "./PointDisplay.css";

export const enum Point {
  Channeled = '∕',
  Exhausted = '⨉',
  Consumed = '※',
  Free = ' '
}

export interface IPointDisplayProps {
  point: Point,
  value: 1 | 0 | -1
}

export const PointDisplay: FunctionComponent<IPointDisplayProps> = ({point, value}) => {
  return <Fragment>
    <span className={classSet({
      PointDisplay: true,
      "PointDisplay-heal": value < 0,
      "PointDisplay-harm": value > 0,
      "PointDisplay-free": value == 0,
      "PointDisplay-channeled": point === Point.Channeled,
      "PointDisplay-exhausted": point === Point.Exhausted,
      "PointDisplay-consumed": point === Point.Consumed,
    })}>{point != Point.Free ? point : <Fragment>&nbsp;</Fragment>}</span>
  </Fragment>
};