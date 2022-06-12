import {PointsVec} from "./PointsBlock";
import "./drag.css"

export const enum ItemTypes {
  PointsPreview = 'PointsPreview'
}

export interface PointsPreviewData {
  points: PointsVec;
  channelingIndex?: number;
}
